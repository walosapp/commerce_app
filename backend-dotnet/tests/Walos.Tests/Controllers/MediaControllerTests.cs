using Microsoft.AspNetCore.Mvc;
using Moq;
using Walos.API.Controllers;
using Walos.Application.Storage;

namespace Walos.Tests.Controllers;

public class MediaControllerTests
{
    [Fact]
    public void GetManagedImage_RedirectsCanonicalManagedProductKey()
    {
        const string key = "companies/16/products/25/abc-123.webp";
        const string publicUrl = "https://project.supabase.co/storage/v1/object/public/walos-public-images/companies/16/products/25/abc-123.webp";
        var storage = new Mock<IFileStorage>();
        storage.Setup(service => service.IsManagedReference(key)).Returns(true);
        storage.Setup(service => service.GetPublicUrl(key)).Returns(publicUrl);
        var controller = new MediaController(storage.Object);

        var result = controller.GetManagedImage(key);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(publicUrl, redirect.Url);
    }

    [Theory]
    [InlineData("https://evil.example/logo.png")]
    [InlineData("companies/16/avatars/user.png")]
    [InlineData("companies/16/branding/../secret.png")]
    [InlineData("companies/16/products/25/nested/image.png")]
    [InlineData("companies/other/branding/logo.png")]
    public void GetManagedImage_RejectsNonCanonicalReferencesWithoutResolvingRedirect(string key)
    {
        var storage = new Mock<IFileStorage>();
        var controller = new MediaController(storage.Object);

        var result = controller.GetManagedImage(key);

        Assert.IsType<BadRequestObjectResult>(result);
        storage.Verify(service => service.GetPublicUrl(It.IsAny<string>()), Times.Never);
    }
}
