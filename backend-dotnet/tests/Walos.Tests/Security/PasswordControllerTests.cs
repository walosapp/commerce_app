using Microsoft.AspNetCore.Mvc;
using Moq;
using Walos.API.Controllers;
using Walos.API.Services;
using Walos.Application.Security;
using Walos.Application.Services;

namespace Walos.Tests.Security;

public sealed class PasswordControllerTests
{
    [Fact]
    public async Task SelfChange_Uses_Authenticated_UserAndTenant_Not_RequestIdentity()
    {
        var authService = new Mock<IAuthService>(MockBehavior.Strict);
        var tenant = new TenantContext
        {
            IsAuthenticated = true,
            UserId = 17,
            CompanyId = 23,
            Role = WalosRoles.Cashier
        };
        authService.Setup(service => service.ChangePasswordAsync(
                17, 23, "Current1!", "Changed2@", "Changed2@"))
            .ReturnsAsync(new TokenResult { Token = "access", RefreshToken = "refresh" });
        var controller = new AuthController(authService.Object, tenant);

        var result = await controller.ChangePassword(
            new AuthController.ChangePasswordRequest("Current1!", "Changed2@", "Changed2@"));

        Assert.IsType<OkObjectResult>(result);
        authService.VerifyAll();
    }

    [Fact]
    public async Task TenantReset_Uses_Authenticated_ActorAndTenant_Not_ClientCompanyId()
    {
        var usersService = new Mock<IUsersService>(MockBehavior.Strict);
        var tenant = new TenantContext
        {
            IsAuthenticated = true,
            UserId = 7,
            CompanyId = 10,
            Role = WalosRoles.Manager
        };
        usersService.Setup(service => service.ResetPasswordAsync(
                10, 7, WalosRoles.Manager, 8, "Changed2@"))
            .ReturnsAsync(true);
        var controller = new UsersController(usersService.Object, tenant);

        var result = await controller.ResetPassword(
            8,
            new UsersController.ResetPasswordRequest("Changed2@"));

        Assert.IsType<OkObjectResult>(result);
        usersService.VerifyAll();
    }
}
