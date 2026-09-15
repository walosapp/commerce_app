using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Walos.API.Controllers;
using Walos.Application.Services;
using Walos.Application.Storage;
using Walos.Domain.Entities;

namespace Walos.Tests.Controllers;

public class PwaControllerTests
{
    [Fact]
    public async Task GetManifest_Should_Use_Frontend_Origin_And_Root_Relative_Backend_Icon_Urls()
    {
        var companyService = new Mock<ICompanyService>();
        companyService
            .Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings
            {
                Id = 16,
                Name = "Comercio 16",
                DisplayName = "Mi comercio",
                LogoUrl = "/uploads/branding/company_16.jpeg"
            });

        var environment = new Mock<IWebHostEnvironment>();
        var controller = new PwaController(
            companyService.Object,
            Mock.Of<IFileStorage>(),
            environment.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("walos-api.up.railway.app");
        controller.Request.Headers.Origin = "https://walos.vercel.app";

        var actionResult = await controller.GetManifest(16, "company_16.jpeg");

        var content = Assert.IsType<ContentResult>(actionResult);
        Assert.Equal("application/manifest+json", content.ContentType);

        using var manifest = JsonDocument.Parse(Assert.IsType<string>(content.Content));
        var root = manifest.RootElement;
        Assert.Equal("https://walos.vercel.app/", root.GetProperty("scope").GetString());
        Assert.Equal("https://walos.vercel.app/", root.GetProperty("start_url").GetString());

        var firstIcon = root.GetProperty("icons").EnumerateArray().First();
        Assert.StartsWith(
            "/api/v1/pwa/icon/16/",
            firstIcon.GetProperty("src").GetString());
    }

    [Fact]
    public async Task GetManifest_Should_Not_Trust_NonHttp_Origin()
    {
        var companyService = new Mock<ICompanyService>();
        companyService
            .Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16" });

        var controller = new PwaController(
            companyService.Object,
            Mock.Of<IFileStorage>(),
            Mock.Of<IWebHostEnvironment>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("walos-api.up.railway.app");
        controller.Request.Headers.Origin = "javascript:alert(1)";

        var actionResult = await controller.GetManifest(16);

        var content = Assert.IsType<ContentResult>(actionResult);
        using var manifest = JsonDocument.Parse(Assert.IsType<string>(content.Content));
        Assert.Equal(
            "https://walos-api.up.railway.app/",
            manifest.RootElement.GetProperty("start_url").GetString());
    }

    [Fact]
    public async Task GetIcon_Should_OpenCanonicalTenantBrandingThroughManagedStorage()
    {
        const string key = "companies/16/branding/logo-0123456789abcdef0123456789abcdef.webp";
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = key });
        var storage = new Mock<IFileStorage>();
        storage.Setup(service => service.IsManagedReference(key)).Returns(true);
        storage.Setup(service => service.TryGetManagedObjectKey(key, out It.Ref<string>.IsAny))
            .Returns((string? _, out string objectKey) =>
            {
                objectKey = key;
                return true;
            });
        storage.Setup(service => service.OpenReadAsync(key, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Stream?>(CreatePngStream()));
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 144);

        await AssertSquarePngAsync(result, 144);
        Assert.Contains("immutable", controller.Response.Headers.CacheControl.ToString());
        storage.Verify(
            service => service.OpenReadAsync(key, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetIcon_Should_ResolveCanonicalManagedPublicUrlToTenantBrandingKey()
    {
        const string key = "companies/16/branding/logo-0123456789abcdef0123456789abcdef.webp";
        const string publicUrl =
            "https://project-ref.supabase.co/storage/v1/object/public/walos-public-images/" + key;
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = publicUrl });
        var storage = new Mock<IFileStorage>();
        storage.Setup(service => service.TryGetManagedObjectKey(publicUrl, out It.Ref<string>.IsAny))
            .Returns((string? _, out string objectKey) =>
            {
                objectKey = key;
                return true;
            });
        storage.Setup(service => service.OpenReadAsync(key, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Stream?>(CreatePngStream()));
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 144);

        await AssertSquarePngAsync(result, 144);
        Assert.Contains("immutable", controller.Response.Headers.CacheControl.ToString());
        storage.Verify(
            service => service.OpenReadAsync(key, It.IsAny<CancellationToken>()),
            Times.Once);
        storage.Verify(
            service => service.OpenReadAsync(publicUrl, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetIcon_Should_UseFallbackForManagedPublicUrlFromAnotherTenant()
    {
        const string key = "companies/17/branding/logo-0123456789abcdef0123456789abcdef.webp";
        const string publicUrl =
            "https://project-ref.supabase.co/storage/v1/object/public/walos-public-images/" + key;
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = publicUrl });
        var storage = new Mock<IFileStorage>();
        storage.Setup(service => service.TryGetManagedObjectKey(publicUrl, out It.Ref<string>.IsAny))
            .Returns((string? _, out string objectKey) =>
            {
                objectKey = key;
                return true;
            });
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 144);

        await AssertSquarePngAsync(result, 144);
        storage.Verify(
            service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetIcon_Should_UseFallbackForManagedPublicUrlTraversalWithoutFetching()
    {
        const string publicUrl =
            "https://project-ref.supabase.co/storage/v1/object/public/walos-public-images/" +
            "companies/16/branding/%2E%2E/secret.webp";
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = publicUrl });
        var storage = new Mock<IFileStorage>();
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 144);

        await AssertSquarePngAsync(result, 144);
        storage.Verify(
            service => service.TryGetManagedObjectKey(publicUrl, out It.Ref<string>.IsAny),
            Times.Once);
        storage.Verify(
            service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetIcon_Should_KeepLegacyBrandingFallback()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), $"walos-pwa-{Guid.NewGuid():N}");
        var brandingDirectory = Path.Combine(webRoot, "uploads", "branding");
        Directory.CreateDirectory(brandingDirectory);
        var logoPath = Path.Combine(brandingDirectory, "legacy.png");
        await using (var logo = CreatePngStream())
        await using (var destination = File.Create(logoPath))
            await logo.CopyToAsync(destination);

        try
        {
            var companyService = new Mock<ICompanyService>();
            companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
                .ReturnsAsync(new CompanySettings
                {
                    Id = 16,
                    Name = "Comercio 16",
                    LogoUrl = "/uploads/branding/legacy.png"
                });
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(value => value.WebRootPath).Returns(webRoot);
            var storage = new Mock<IFileStorage>();
            var controller = CreateController(companyService.Object, storage.Object, environment.Object);

            var result = await controller.GetIcon(16, 144);

            await AssertSquarePngAsync(result, 144);
            storage.Verify(
                service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetIcon_Should_UseFallbackForArbitraryExternalLogoWithoutDownloadingIt()
    {
        const string externalUrl = "https://evil.example/logo.png";
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = externalUrl });
        var storage = new Mock<IFileStorage>();
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 144);

        await AssertSquarePngAsync(result, 144);
        storage.Verify(
            service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetIcon_Should_UseFallbackForLegacyPathTraversal()
    {
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings
            {
                Id = 16,
                Name = "Comercio 16",
                LogoUrl = "/uploads/branding/../secret.png"
            });
        var storage = new Mock<IFileStorage>();
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 144);

        await AssertSquarePngAsync(result, 144);
        storage.Verify(
            service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("default")]
    public async Task GetIcon_Should_UseFallback_WhenLogoIsNotConfigured(string? logoReference)
    {
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = logoReference });
        var storage = new Mock<IFileStorage>();
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 192);

        await AssertSquarePngAsync(result, 192);
        storage.Verify(
            service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetIcon_Should_UseFallback_WhenCanonicalBrandingIsMissing()
    {
        const string key = "companies/16/branding/logo-0123456789abcdef0123456789abcdef.webp";
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = key });
        var storage = new Mock<IFileStorage>();
        storage.Setup(service => service.TryGetManagedObjectKey(key, out It.Ref<string>.IsAny))
            .Returns((string? _, out string objectKey) =>
            {
                objectKey = key;
                return true;
            });
        storage.Setup(service => service.OpenReadAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 384);

        await AssertSquarePngAsync(result, 384);
        Assert.DoesNotContain("immutable", controller.Response.Headers.CacheControl.ToString());
        storage.Verify(
            service => service.OpenReadAsync(key, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetIcon_Should_UseFallback_WhenLegacyBrandingIsMissing()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), $"walos-pwa-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "branding"));

        try
        {
            var companyService = new Mock<ICompanyService>();
            companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
                .ReturnsAsync(new CompanySettings
                {
                    Id = 16,
                    Name = "Comercio 16",
                    LogoUrl = "/uploads/branding/missing.png"
                });
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(value => value.WebRootPath).Returns(webRoot);
            var storage = new Mock<IFileStorage>();
            var controller = CreateController(companyService.Object, storage.Object, environment.Object);

            var result = await controller.GetIcon(16, 512);

            await AssertSquarePngAsync(result, 512);
            storage.Verify(
                service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(72)]
    [InlineData(96)]
    [InlineData(128)]
    [InlineData(144)]
    [InlineData(152)]
    [InlineData(180)]
    [InlineData(192)]
    [InlineData(384)]
    [InlineData(512)]
    public async Task GetIcon_Fallback_Should_MatchRequestedDimensions(int size)
    {
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16" });
        var controller = CreateController(
            companyService.Object,
            Mock.Of<IFileStorage>(),
            Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, size);

        await AssertSquarePngAsync(result, size);
    }

    private static PwaController CreateController(
        ICompanyService companyService,
        IFileStorage storage,
        IWebHostEnvironment environment) =>
        new(companyService, storage, environment)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private static MemoryStream CreatePngStream()
    {
        using var image = new Image<Rgba32>(2, 2, Color.White);
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }

    private static async Task AssertSquarePngAsync(IActionResult result, int expectedSize)
    {
        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", file.ContentType);
        using var image = await Image.LoadAsync<Rgba32>(file.FileStream);
        Assert.Equal(expectedSize, image.Width);
        Assert.Equal(expectedSize, image.Height);
    }
}
