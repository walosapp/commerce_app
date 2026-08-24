using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Walos.API.Controllers;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Controllers;

public class CompanyControllerTests
{
    [Fact]
    public async Task UploadLogo_UsesAuthenticatedTenant_AndReturnsPublicUrl()
    {
        const long companyId = 16;
        const long userId = 81;
        const string publicUrl = "https://storage.example/public/companies/16/branding/logo.png";
        var service = new Mock<ICompanyService>();
        service.Setup(s => s.UploadLogoAsync(
                companyId,
                userId,
                It.IsAny<Stream>(),
                "logo.png",
                "image/png"))
            .ReturnsAsync(publicUrl);
        var tenant = CreateTenant(companyId, userId);
        var controller = new CompanyController(service.Object, tenant.Object);
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "logo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var action = await controller.UploadLogo(file);

        var ok = Assert.IsType<OkObjectResult>(action);
        var json = JsonSerializer.Serialize(ok.Value);
        using var response = JsonDocument.Parse(json);
        Assert.Equal(publicUrl, response.RootElement.GetProperty("Data").GetProperty("logoUrl").GetString());
        service.VerifyAll();
    }

    [Fact]
    public async Task RemoveLogo_UsesAuthenticatedTenant()
    {
        const long companyId = 16;
        const long userId = 81;
        var service = new Mock<ICompanyService>();
        service.Setup(s => s.RemoveLogoAsync(companyId, userId)).Returns(Task.CompletedTask);
        var tenant = CreateTenant(companyId, userId);
        var controller = new CompanyController(service.Object, tenant.Object);

        var action = await controller.RemoveLogo();

        Assert.IsType<OkObjectResult>(action);
        service.VerifyAll();
    }

    private static Mock<ITenantContext> CreateTenant(long companyId, long userId)
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.CompanyId).Returns(companyId);
        tenant.SetupGet(t => t.UserId).Returns(userId);
        return tenant;
    }
}
