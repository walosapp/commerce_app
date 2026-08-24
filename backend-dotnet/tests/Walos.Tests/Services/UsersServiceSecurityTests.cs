using Moq;
using Walos.Application.DTOs.Users;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Services;

public class UsersServiceSecurityTests
{
    private readonly Mock<IUsersRepository> _repository = new();

    [Fact]
    public async Task Create_Rejects_Role_From_Another_Tenant()
    {
        var service = new UsersService(_repository.Object);
        SetupActorRole();
        _repository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
        _repository.Setup(r => r.GetRoleForAssignmentAsync(99, 10)).ReturnsAsync((RoleAssignmentInfo?)null);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            10, 7, WalosRoles.Manager, Request(roleId: 99, branchId: null)));

        _repository.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Create_Rejects_Branch_From_Another_Tenant()
    {
        var service = new UsersService(_repository.Object);
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
        var service = new UsersService(_repository.Object);
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
        var service = new UsersService(_repository.Object);
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

    private void SetupActorRole()
    {
        _repository.Setup(r => r.GetUserRoleForAssignmentAsync(7, 10))
            .ReturnsAsync(new RoleAssignmentInfo(1, WalosRoles.Manager, 8));
    }

    private static CreateUserRequest Request(long roleId, long? branchId) => new()
    {
        FirstName = "New",
        LastName = "User",
        Email = $"user-{Guid.NewGuid():N}@test.local",
        Password = "password123",
        RoleId = roleId,
        BranchId = branchId,
    };
}
