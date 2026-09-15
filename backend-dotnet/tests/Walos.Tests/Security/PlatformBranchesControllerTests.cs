using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Walos.API.Controllers;
using Walos.API.Services;
using Walos.Application.DTOs.Admin;
using Walos.Application.DTOs.Common;
using Walos.Application.Security;
using Walos.Application.Services;

namespace Walos.Tests.Security;

public sealed class PlatformBranchesControllerTests
{
    [Fact]
    public void Controller_Requires_Platform_Admin()
    {
        var authorize = Assert.Single(typeof(PlatformBranchesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(WalosPolicies.PlatformAdmin, authorize.Policy);
    }

    [Fact]
    public async Task Create_Uses_Route_Company_And_Trusted_Actor()
    {
        var request = new CreateBranchAdminRequest
        {
            Name = "Norte", Code = "NORTE", Address = "Calle 1", City = "Bogota"
        };
        var expected = Services.AdminBranchServiceTests.Branch(10, 42, "NORTE", true);
        var service = new Mock<IAdminService>();
        service.Setup(x => x.CreateBranchAsync(42, request, 77)).ReturnsAsync(expected);
        var controller = new PlatformBranchesController(service.Object,
            new TenantContext { CompanyId = 1, UserId = 77, IsPlatformAdmin = true });

        var result = await controller.CreateBranch(42, request);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<ApiResponse<BranchAdminResponse>>(created.Value);
        Assert.Same(expected, response.Data);
        service.VerifyAll();
    }
}
