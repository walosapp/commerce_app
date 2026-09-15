using Npgsql;
using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class UsersRepositorySecurityIntegrationTests : V1IntegrationTestBase
{
    [SkippableFact]
    public async Task Role_And_Branch_From_Another_Company_Are_Not_Assignable()
    {
        var companyA = await SeedCompanyAsync("Users Tenant A");
        var companyB = await SeedCompanyAsync("Users Tenant B");
        var branchB = await SeedBranchAsync(companyB, "Branch B");
        var roleB = await SeedRoleAsync(companyB, "cashier", 5);

        Assert.Null(await UsersRepository.GetRoleForAssignmentAsync(roleB, companyA));
        Assert.False(await UsersRepository.IsActiveBranchInCompanyAsync(branchB, companyA));

        var user = new User
        {
            CompanyId = companyA,
            BranchId = branchB,
            RoleId = roleB,
            FirstName = "Cross",
            LastName = "Tenant",
            Email = $"cross-{Guid.NewGuid():N}@test.local",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UsersRepository.CreateAsync(user, BCrypt.Net.BCrypt.HashPassword("password123")));
    }

    [SkippableFact]
    public async Task Read_Queries_Do_Not_Expose_Historically_Malformed_Cross_Tenant_Relations()
    {
        var companyA = await SeedCompanyAsync("Users Read Tenant A");
        var companyB = await SeedCompanyAsync("Users Read Tenant B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");
        var branchB = await SeedBranchAsync(companyB, "Branch B");
        var roleB = await SeedRoleAsync(companyB, "cashier", 5);
        var userA = await SeedUserAsync(companyA, branchA, $"malformed-{Guid.NewGuid():N}@test.local");

        using (var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync())
        using (var command = new NpgsqlCommand(@"
            UPDATE core.users
            SET role_id = @roleB, branch_id = @branchB
            WHERE id = @userA AND company_id = @companyA", conn))
        {
            command.Parameters.AddWithValue("roleB", roleB);
            command.Parameters.AddWithValue("branchB", branchB);
            command.Parameters.AddWithValue("userA", userA);
            command.Parameters.AddWithValue("companyA", companyA);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        Assert.Null(await UsersRepository.GetByIdAsync(userA, companyA));
        Assert.DoesNotContain(await UsersRepository.GetAllAsync(companyA), user => user.Id == userA);
    }

    [SkippableFact]
    public async Task Existing_Dev_User_Cannot_Be_Reassigned_Or_SoftDeleted()
    {
        var companyId = await SeedCompanyAsync("Protected dev mutation");
        var userId = await SeedUserAsync(companyId, null);
        var managerRoleId = (await UsersRepository.GetByIdAsync(userId, companyId))!.RoleId;
        var devRoleId = await SeedRoleAsync(companyId, "dev", 100);

        using (var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync())
        using (var command = new NpgsqlCommand(@"
            UPDATE core.users SET role_id = @devRoleId
            WHERE id = @userId AND company_id = @companyId", conn))
        {
            command.Parameters.AddWithValue("devRoleId", devRoleId);
            command.Parameters.AddWithValue("userId", userId);
            command.Parameters.AddWithValue("companyId", companyId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        var updated = await UsersRepository.UpdateAsync(new User
        {
            Id = userId,
            CompanyId = companyId,
            RoleId = managerRoleId,
            FirstName = "Changed",
            LastName = "Role",
        });
        var deleted = await UsersRepository.SoftDeleteAsync(userId, companyId);

        Assert.Null(updated);
        Assert.False(deleted);
        var persisted = await UsersRepository.GetByIdAsync(userId, companyId);
        Assert.NotNull(persisted);
        Assert.Equal("dev", persisted!.RoleCode);
        Assert.Null(persisted.DeletedAt);
    }

    private async Task<long> SeedRoleAsync(long companyId, string code, int accessLevel)
    {
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        using var command = new NpgsqlCommand(@"
            INSERT INTO core.roles (company_id, name, code, access_level, is_active)
            VALUES (@companyId, @code, @code, @accessLevel, TRUE)
            RETURNING id", conn);
        command.Parameters.AddWithValue("companyId", companyId);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("accessLevel", accessLevel);
        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Role seed failed"));
    }
}
