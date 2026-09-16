using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Walos.Infrastructure.Repositories;

namespace Walos.Tests.Integration;

[Collection("PostgreSQL Integration Tests")]
public sealed class PasswordRepositoryIntegrationTests : V1IntegrationTestBase
{
    [SkippableFact]
    public async Task SelfChange_IsTenantScoped_And_AtomicallyRotatesRefreshToken()
    {
        var companyId = await SeedCompanyAsync("Password self owner");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId, password: "Current1!");
        var foreignCompanyId = await SeedCompanyAsync("Password self foreign");
        const string refreshToken = "password-self-refresh-token";
        await AuthRepository.SaveRefreshTokenAsync(userId, refreshToken, DateTime.UtcNow.AddDays(1));

        var rejected = await AuthRepository.ChangePasswordAndRotateRefreshTokenAsync(
            userId,
            foreignCompanyId,
            BCrypt.Net.BCrypt.HashPassword("Current1!"),
            BCrypt.Net.BCrypt.HashPassword("Rejected2@"),
            "rejected-new-refresh",
            DateTime.UtcNow.AddDays(1));

        Assert.False(rejected);
        Assert.NotNull(await AuthRepository.GetUserByRefreshTokenAsync(refreshToken));

        const string newRefreshToken = "password-self-new-refresh-token";
        var changed = await AuthRepository.ChangePasswordAndRotateRefreshTokenAsync(
            userId,
            companyId,
            (await AuthRepository.GetUserForPasswordChangeAsync(userId, companyId))!.PasswordHash,
            BCrypt.Net.BCrypt.HashPassword("Changed2@"),
            newRefreshToken,
            DateTime.UtcNow.AddDays(1));

        Assert.True(changed);
        Assert.Null(await AuthRepository.GetUserByRefreshTokenAsync(refreshToken));
        Assert.NotNull(await AuthRepository.GetUserByRefreshTokenAsync(newRefreshToken));
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        var storedHash = await connection.QuerySingleAsync<string>(
            "SELECT password_hash FROM core.users WHERE id = @userId AND company_id = @companyId",
            new { userId, companyId });
        Assert.True(BCrypt.Net.BCrypt.Verify("Changed2@", storedHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("Current1!", storedHash));
    }

    [SkippableFact]
    public async Task TenantAdminReset_IsTenantScoped_ProtectsRefreshSession_AndChangesOnlyTarget()
    {
        var companyId = await SeedCompanyAsync("Password reset owner");
        var branchId = await SeedBranchAsync(companyId);
        var targetUserId = await SeedUserAsync(companyId, branchId, password: "Current1!");
        var actorUserId = await SeedUserAsync(companyId, branchId, password: "Actor1!x");
        var otherUserId = await SeedUserAsync(companyId, branchId, password: "Other1!x");
        var foreignCompanyId = await SeedCompanyAsync("Password reset foreign");
        const string refreshToken = "password-admin-reset-token";
        await AuthRepository.SaveRefreshTokenAsync(targetUserId, refreshToken, DateTime.UtcNow.AddDays(1));
        var repository = new UsersRepository(
            ConnectionFactory,
            NullLogger<UsersRepository>.Instance);

        var rejected = await repository.ResetPasswordByTenantActorAsync(
            actorUserId,
            "manager",
            targetUserId,
            foreignCompanyId,
            BCrypt.Net.BCrypt.HashPassword("Rejected2@"));
        Assert.False(rejected);
        Assert.NotNull(await AuthRepository.GetUserByRefreshTokenAsync(refreshToken));

        var changed = await repository.ResetPasswordByTenantActorAsync(
            actorUserId,
            "manager",
            targetUserId,
            companyId,
            BCrypt.Net.BCrypt.HashPassword("Changed2@"));

        Assert.True(changed);
        Assert.Null(await AuthRepository.GetUserByRefreshTokenAsync(refreshToken));
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        var hashes = (await connection.QueryAsync<(long Id, string PasswordHash)>(@"
            SELECT id AS Id, password_hash AS PasswordHash
            FROM core.users
            WHERE id IN (@targetUserId, @otherUserId)",
            new { targetUserId, otherUserId })).ToDictionary(row => row.Id, row => row.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Changed2@", hashes[targetUserId]));
        Assert.True(BCrypt.Net.BCrypt.Verify("Other1!x", hashes[otherUserId]));
    }
}
