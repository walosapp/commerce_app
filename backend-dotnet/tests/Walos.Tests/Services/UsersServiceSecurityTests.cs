using Moq;
using Walos.Application.DTOs.Users;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Walos.Tests.Services;

public class UsersServiceSecurityTests
{
    private readonly Mock<IUsersRepository> _repository = new();

    [Fact]
    public async Task Create_Rejects_Role_From_Another_Tenant()
    {
        var service = CreateService();
        SetupActorRole();
        _repository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
        _repository.Setup(r => r.GetRoleForAssignmentAsync(99, 10)).ReturnsAsync((RoleAssignmentInfo?)null);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            10, 7, WalosRoles.Manager, Request(roleId: 99, branchId: null)));

        _repository.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("password123")]
    [InlineData("Ábcdef1!")]
    public async Task Create_Rejects_PasswordOutsideSharedPolicy_BeforeRepositoryAccess(string password)
    {
        var service = CreateService();
        var request = Request(roleId: 3, branchId: null);
        request.Password = password;

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            10, 7, WalosRoles.Manager, request));

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_Rejects_Branch_From_Another_Tenant()
    {
        var service = CreateService();
        SetupActorRole();
        _repository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
        _repository.Setup(r => r.GetRoleForAssignmentAsync(3, 10))
            .ReturnsAsync(new RoleAssignmentInfo(3, WalosRoles.Cashier, 5));
        _repository.Setup(r => r.IsActiveBranchInCompanyAsync(88, 10)).ReturnsAsync(false);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            10, 7, WalosRoles.Manager, Request(roleId: 3, branchId: 88)));

        _repository.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Update_Rejects_Manager_Self_Elevation_To_SuperAdmin()
    {
        var service = CreateService();
        SetupActorRole();
        _repository.Setup(r => r.GetByIdAsync(7, 10)).ReturnsAsync(new User { Id = 7, CompanyId = 10 });
        _repository.Setup(r => r.GetRoleForAssignmentAsync(2, 10))
            .ReturnsAsync(new RoleAssignmentInfo(2, WalosRoles.SuperAdmin, 10));

        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(
            10,
            7,
            WalosRoles.Manager,
            7,
            new UpdateUserRequest
            {
                FirstName = "Manager",
                LastName = "Tenant",
                RoleId = 2,
            }));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Create_Allows_Manager_To_Assign_Lower_Active_Role_And_Tenant_Branch()
    {
        var service = CreateService();
        SetupActorRole();
        _repository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
        _repository.Setup(r => r.GetRoleForAssignmentAsync(3, 10))
            .ReturnsAsync(new RoleAssignmentInfo(3, WalosRoles.Cashier, 5));
        _repository.Setup(r => r.IsActiveBranchInCompanyAsync(20, 10)).ReturnsAsync(true);
        _repository.Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync((User user, string _) => user);

        var result = await service.CreateAsync(
            10, 7, WalosRoles.Manager, Request(roleId: 3, branchId: 20));

        Assert.Equal(10, result.CompanyId);
        Assert.Equal(20, result.BranchId);
        Assert.Equal(3, result.RoleId);
    }

    [Fact]
    public async Task ResetPassword_Rejects_Self_Reset_Before_Repository_Access()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.ResetPasswordAsync(
            10, 7, WalosRoles.Manager, 7, "Changed2@"));

        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("password123")]
    [InlineData("Ábcdef1!")]
    public async Task ResetPassword_Rejects_PasswordOutsideSharedPolicy_BeforeRepositoryAccess(string password)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.ResetPasswordAsync(
            10, 7, WalosRoles.Manager, 8, password));

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResetPassword_Is_TenantScoped_And_Does_Not_Write_When_Target_Is_Missing()
    {
        var service = CreateService();
        SetupActorRole();
        _repository.Setup(repository => repository.GetByIdAsync(88, 10)).ReturnsAsync((User?)null);

        var result = await service.ResetPasswordAsync(
            10, 7, WalosRoles.Manager, 88, "Changed2@");

        Assert.False(result);
        _repository.Verify(repository => repository.GetByIdAsync(88, 10), Times.Once);
        _repository.Verify(repository => repository.ResetPasswordByTenantActorAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_Hashes_NewPassword_And_Uses_ActorAndTargetTenantScope()
    {
        var service = CreateService();
        string? storedHash = null;
        SetupActorRole();
        _repository.Setup(repository => repository.GetByIdAsync(8, 10))
            .ReturnsAsync(new User { Id = 8, CompanyId = 10, RoleCode = WalosRoles.Cashier });
        _repository.Setup(repository => repository.ResetPasswordByTenantActorAsync(
                7, WalosRoles.Manager, 8, 10, It.IsAny<string>()))
            .Callback<long, string, long, long, string>((_, _, _, _, hash) => storedHash = hash)
            .ReturnsAsync(true);

        var result = await service.ResetPasswordAsync(
            10, 7, WalosRoles.Manager, 8, "Changed2@");

        Assert.True(result);
        _repository.Verify(repository => repository.ResetPasswordByTenantActorAsync(
            7, WalosRoles.Manager, 8, 10, It.IsAny<string>()), Times.Once);
        Assert.NotNull(storedHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Changed2@", storedHash));
    }

    [Fact]
    public async Task ResetPassword_FailsClosed_When_TargetIsPromotedBeforeAtomicUpdate()
    {
        var service = CreateService();
        SetupActorRole();
        _repository.Setup(repository => repository.GetByIdAsync(8, 10))
            .ReturnsAsync(new User { Id = 8, CompanyId = 10, RoleCode = WalosRoles.Cashier });
        _repository.Setup(repository => repository.ResetPasswordByTenantActorAsync(
                7, WalosRoles.Manager, 8, 10, It.IsAny<string>()))
            .ReturnsAsync(false);

        var changed = await service.ResetPasswordAsync(
            10, 7, WalosRoles.Manager, 8, "Changed2@");

        Assert.False(changed);
        _repository.Verify(repository => repository.ResetPasswordByTenantActorAsync(
            7, WalosRoles.Manager, 8, 10, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_Preserves_Dev_Account_Protection()
    {
        var service = CreateService();
        SetupActorRole();
        _repository.Setup(repository => repository.GetByIdAsync(8, 10))
            .ReturnsAsync(new User { Id = 8, CompanyId = 10, RoleCode = WalosRoles.Dev });

        var error = await Assert.ThrowsAsync<BusinessException>(() => service.ResetPasswordAsync(
            10, 7, WalosRoles.Manager, 8, "Changed2@"));

        Assert.Equal("protected_technical_account", error.Code);
        _repository.Verify(repository => repository.ResetPasswordByTenantActorAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    private void SetupActorRole()
    {
        _repository.Setup(r => r.GetUserRoleForAssignmentAsync(7, 10))
            .ReturnsAsync(new RoleAssignmentInfo(1, WalosRoles.Manager, 8));
    }

    private UsersService CreateService() =>
        new(_repository.Object, NullLogger<UsersService>.Instance);

    private static CreateUserRequest Request(long roleId, long? branchId) => new()
    {
        FirstName = "New",
        LastName = "User",
        Email = $"user-{Guid.NewGuid():N}@test.local",
        Password = "Changed2@",
        RoleId = roleId,
        BranchId = branchId,
    };
}
