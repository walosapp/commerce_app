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
        const string key = "companies/16/branding/logo.webp";
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = key });
        var storage = new Mock<IFileStorage>();
        storage.Setup(service => service.IsManagedReference(key)).Returns(true);
        storage.Setup(service => service.OpenReadAsync(key, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Stream?>(CreatePngStream()));
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 144);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", file.ContentType);
        storage.Verify(
            service => service.OpenReadAsync(key, It.IsAny<CancellationToken>()),
            Times.Once);
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

            Assert.IsType<FileStreamResult>(result);
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
    public async Task GetIcon_Should_RejectArbitraryExternalLogoWithoutDownloadingIt()
    {
        const string externalUrl = "https://evil.example/logo.png";
        var companyService = new Mock<ICompanyService>();
        companyService.Setup(service => service.GetSettingsWithRawLogoAsync(16))
            .ReturnsAsync(new CompanySettings { Id = 16, Name = "Comercio 16", LogoUrl = externalUrl });
        var storage = new Mock<IFileStorage>();
        var controller = CreateController(companyService.Object, storage.Object, Mock.Of<IWebHostEnvironment>());

        var result = await controller.GetIcon(16, 144);

        Assert.IsType<NotFoundObjectResult>(result);
        storage.Verify(
            service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetIcon_Should_RejectLegacyPathTraversal()
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

        Assert.IsType<NotFoundObjectResult>(result);
        storage.Verify(
            service => service.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
}
