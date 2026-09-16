using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.API.Controllers;
using Walos.API.Services;
using Walos.Application.DTOs.Admin;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Security;

public sealed class PasswordCreationPolicyTests
{
    [Theory]
    [InlineData("password123")]
    [InlineData("Ábcdef1!")]
    public async Task TenantCreation_RejectsPasswordOutsideSharedPolicy(string password)
    {
        var repository = new Mock<IAdminRepository>(MockBehavior.Strict);
        var service = new AdminService(repository.Object, NullLogger<AdminService>.Instance);
        var request = new CreateTenantRequest
        {
            CompanyName = "Tenant",
            AdminEmail = "admin@test.local",
            AdminPassword = password,
            BranchName = "Main"
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateTenantAsync(request));

        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("password123")]
    [InlineData("Ábcdef1!")]
    public async Task PlatformUserCreation_RejectsPasswordOutsideSharedPolicy(string password)
    {
        var adminService = new Mock<IAdminService>(MockBehavior.Strict);
        var usersRepository = new Mock<IUsersRepository>(MockBehavior.Strict);
        var controller = new AdminController(
            adminService.Object,
            usersRepository.Object,
            new TenantContext
            {
                IsAuthenticated = true,
                UserId = 99,
                CompanyId = 1,
                Role = WalosRoles.PlatformAdmin
            },
            NullLogger<AdminController>.Instance);
        var request = new AdminController.CreateUserInTenantRequest(
            10,
            3,
            null,
            "New",
            "User",
            "new@test.local",
            password,
            null);

        await Assert.ThrowsAsync<ValidationException>(() => controller.CreateUserInTenant(request));

        usersRepository.VerifyNoOtherCalls();
        adminService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("password123")]
    [InlineData("Ábcdef1!")]
    public async Task TenantAdminReset_RejectsPasswordOutsideSharedPolicy(string password)
    {
        var repository = new Mock<IAdminRepository>(MockBehavior.Strict);
        var service = new AdminService(repository.Object, NullLogger<AdminService>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetTenantAdminPasswordAsync(10, 99, password));

        repository.VerifyNoOtherCalls();
    }
}
