using Walos.Domain.Entities;
using Xunit;

namespace Walos.Tests.Integration;

public class AuthRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetUserByEmailAsync_ReturnsUser_WhenExists()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Auth Test Co");
        var branchId = await SeedBranchAsync(companyId);
        var expectedEmail = "authuser@test.com";
        await SeedUserAsync(companyId, branchId, expectedEmail, "mypassword");

        // Act
        var user = await AuthRepository.GetUserByEmailAsync(expectedEmail);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(expectedEmail, user.Email);
        Assert.Equal(companyId, user.CompanyId);
        Assert.Equal(branchId, user.BranchId);
    }

    [SkippableFact]
    public async Task GetUserByEmailAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var user = await AuthRepository.GetUserByEmailAsync("nonexistent@nowhere.com");

        // Assert
        Assert.Null(user);
    }

    [SkippableFact]
    public async Task SaveAndGetRefreshToken_Works()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Refresh Token Co");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId, "refresh@test.com");
        var refreshToken = "test-refresh-token-12345";
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        await AuthRepository.SaveRefreshTokenAsync(userId, refreshToken, expiresAt);
        var user = await AuthRepository.GetUserByRefreshTokenAsync(refreshToken);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(userId, user.Id);
        Assert.Equal("refresh@test.com", user.Email);
    }

    [SkippableFact]
    public async Task GetUserByRefreshToken_ReturnsNull_WhenTokenExpired()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Expired Token Co");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId, "expired@test.com");
        var refreshToken = "expired-token-12345";
        var expiresAt = DateTime.UtcNow.AddDays(-1); // Expired

        await AuthRepository.SaveRefreshTokenAsync(userId, refreshToken, expiresAt);

        // Act
        var user = await AuthRepository.GetUserByRefreshTokenAsync(refreshToken);

        // Assert
        Assert.Null(user);
    }

    [SkippableFact]
    public async Task LockUserAsync_SetsLockout()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Lockout Co");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId, "lockout@test.com");
        var lockoutTime = DateTime.UtcNow.AddMinutes(15);

        // Act
        await AuthRepository.LockUserAsync(userId, lockoutTime);
        var user = await AuthRepository.GetUserByEmailAsync("lockout@test.com");

        // Assert
        Assert.NotNull(user);
        Assert.NotNull(user.LockedUntil);
        Assert.True(user.LockedUntil > DateTime.UtcNow);
    }

    [SkippableFact]
    public async Task IncrementFailedLoginAsync_IncrementsCounter()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Failed Login Co");
        var branchId = await SeedBranchAsync(companyId);
        await SeedUserAsync(companyId, branchId, "failed@test.com");
        var initialUser = await AuthRepository.GetUserByEmailAsync("failed@test.com");
        var initialAttempts = initialUser!.FailedLoginAttempts;

        // Act
        await AuthRepository.IncrementFailedLoginAsync(initialUser.Id);
        var updatedUser = await AuthRepository.GetUserByEmailAsync("failed@test.com");

        // Assert
        Assert.Equal(initialAttempts + 1, updatedUser!.FailedLoginAttempts);
    }

    [SkippableFact]
    public async Task ResetFailedLoginAsync_ClearsCounter()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Reset Login Co");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId, "reset@test.com");
        await AuthRepository.IncrementFailedLoginAsync(userId);
        await AuthRepository.IncrementFailedLoginAsync(userId);

        // Act
        await AuthRepository.ResetFailedLoginAsync(userId);
        var user = await AuthRepository.GetUserByEmailAsync("reset@test.com");

        // Assert
        Assert.Equal(0, user!.FailedLoginAttempts);
    }

    [SkippableFact]
    public async Task UpdateLastLoginAsync_SetsTimestamp()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Last Login Co");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId, "lastlogin@test.com");
        var beforeUpdate = DateTime.UtcNow;

        // Act
        await AuthRepository.UpdateLastLoginAsync(userId, "127.0.0.1");
        var user = await AuthRepository.GetUserByEmailAsync("lastlogin@test.com");

        // Assert
        Assert.NotNull(user!.LastLoginAt);
        Assert.True(user.LastLoginAt >= beforeUpdate);
        Assert.Equal("127.0.0.1", user.LastLoginIp);
    }

    [SkippableFact]
    public async Task LogoutAsync_RevokesToken()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Logout Co");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId, "logout@test.com");
        var token = "token-to-revoke";
        await AuthRepository.SaveRefreshTokenAsync(userId, token, DateTime.UtcNow.AddDays(7));

        // Act - revoke by setting empty token and past expiration
        await AuthRepository.SaveRefreshTokenAsync(userId, string.Empty, DateTime.UtcNow.AddDays(-1));
        var user = await AuthRepository.GetUserByRefreshTokenAsync(token);

        // Assert
        Assert.Null(user);
    }
}
