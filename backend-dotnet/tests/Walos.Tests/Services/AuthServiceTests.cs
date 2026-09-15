using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Security;
using System.IdentityModel.Tokens.Jwt;

namespace Walos.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IAuthRepository> _repoMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _repoMock = new Mock<IAuthRepository>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "super-secret-key-for-testing-must-be-at-least-32-chars!",
                ["Jwt:ExpiresInMinutes"] = "60",
                ["Jwt:RefreshExpiresInDays"] = "7"
            })
            .Build();
        _service = new AuthService(_repoMock.Object, _configuration, _loggerMock.Object);
    }

    // ── LoginAsync ──

    [Theory]
    [InlineData("", "pass")]
    [InlineData("user", "")]
    [InlineData("  ", "pass")]
    [InlineData("user", "   ")]
    public async Task Login_ThrowsValidation_WhenCredentialsEmpty(string user, string pass)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.LoginAsync(user, pass, null));
    }

    [Fact]
    public async Task Login_ThrowsBusiness_WhenUserNotFound()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync("nobody@test.com")).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.LoginAsync("nobody@test.com", "pass", null));

        Assert.Contains("Credenciales", ex.Message);
    }

    [Fact]
    public async Task Login_ThrowsBusiness_WhenUsernameNotFoundAndNoAt()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync("admin")).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<BusinessException>(() =>
            _service.LoginAsync("admin", "pass", null));
    }

    [Fact]
    public async Task Login_ThrowsBusiness_WhenAccountLocked()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync("locked@test.com"))
            .ReturnsAsync(new User
            {
                Id = 1, Email = "locked@test.com", IsActive = true,
                LockedUntil = DateTime.UtcNow.AddMinutes(10),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass")
            });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.LoginAsync("locked@test.com", "pass", null));

        Assert.Contains("bloqueada", ex.Message);
    }

    [Fact]
    public async Task Login_ThrowsBusiness_WhenAccountInactive()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync("inactive@test.com"))
            .ReturnsAsync(new User
            {
                Id = 2, Email = "inactive@test.com", IsActive = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass")
            });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.LoginAsync("inactive@test.com", "pass", null));

        Assert.Contains("desactivada", ex.Message);
    }

    [Fact]
    public async Task Login_ThrowsBusiness_WhenWrongPassword()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync("user@test.com"))
            .ReturnsAsync(new User
            {
                Id = 3, Email = "user@test.com", IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct"),
                FailedLoginAttempts = 0
            });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.LoginAsync("user@test.com", "wrong", null));

        Assert.Contains("Credenciales", ex.Message);
        _repoMock.Verify(r => r.IncrementFailedLoginAsync(3), Times.Once);
    }

    [Fact]
    public async Task Login_LocksAccount_WhenMaxAttemptsReached()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync("user@test.com"))
            .ReturnsAsync(new User
            {
                Id = 4, Email = "user@test.com", IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct"),
                FailedLoginAttempts = 4
            });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.LoginAsync("user@test.com", "wrong", null));

        Assert.Contains("bloqueada", ex.Message);
        _repoMock.Verify(r => r.LockUserAsync(4, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Login_Success_ReturnsTokenAndUser()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("mypass");
        _repoMock.Setup(r => r.GetUserByEmailAsync("ok@test.com"))
            .ReturnsAsync(new User
            {
                Id = 5, Email = "ok@test.com", IsActive = true,
                PasswordHash = hash, CompanyId = 1, BranchId = 10,
                FirstName = "Ana", LastName = "Lopez", RoleCode = WalosRoles.Manager
            });

        var result = await _service.LoginAsync("ok@test.com", "mypass", "127.0.0.1");

        Assert.NotEmpty(result.Token);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal("Ana Lopez", result.User.Name);
        Assert.Equal(WalosRoles.Manager, result.User.Role);
        Assert.Equal(1, result.User.CompanyId);

        _repoMock.Verify(r => r.ResetFailedLoginAsync(5), Times.Once);
        _repoMock.Verify(r => r.UpdateLastLoginAsync(5, "127.0.0.1"), Times.Once);
        _repoMock.Verify(r => r.SaveRefreshTokenAsync(5, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("owner")]
    [InlineData(null)]
    public async Task Login_Rejects_NonCanonical_Role_Without_Issuing_Tokens(string? role)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("mypass");
        _repoMock.Setup(r => r.GetUserByEmailAsync("legacy@test.com"))
            .ReturnsAsync(new User
            {
                Id = 51,
                Email = "legacy@test.com",
                IsActive = true,
                PasswordHash = hash,
                CompanyId = 1,
                RoleCode = role,
            });

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.LoginAsync("legacy@test.com", "mypass", null));

        Assert.Equal("unsupported_role", error.Code);
        _repoMock.Verify(r => r.ResetFailedLoginAsync(It.IsAny<long>()), Times.Never);
        _repoMock.Verify(r => r.UpdateLastLoginAsync(It.IsAny<long>(), It.IsAny<string?>()), Times.Never);
        _repoMock.Verify(r => r.SaveRefreshTokenAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Theory]
    [InlineData(WalosRoles.Dev, "WALOS-SYSTEM-001", "true", true)]
    [InlineData(WalosRoles.PlatformAdmin, "WALOS-SYSTEM-001", "true", true)]
    [InlineData(WalosRoles.Dev, "TENANT-OTHER", "false", false)]
    [InlineData(WalosRoles.PlatformAdmin, "TENANT-OTHER", "false", false)]
    [InlineData(WalosRoles.SuperAdmin, "WALOS-SYSTEM-001", "false", false)]
    [InlineData(WalosRoles.SuperAdmin, "TENANT-OTHER", "false", false)]
    public async Task Login_PlatformAdmin_Claim_And_UserInfo_Use_The_Same_Trusted_Server_Condition(
        string role,
        string companyTaxId,
        string expectedClaim,
        bool expectedUserInfo)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("mypass");
        _repoMock.Setup(r => r.GetUserByEmailAsync("dev@test.com"))
            .ReturnsAsync(new User
            {
                Id = 50,
                Email = "dev@test.com",
                IsActive = true,
                PasswordHash = hash,
                CompanyId = 10,
                BranchId = 20,
                RoleCode = role,
                CompanyTaxId = companyTaxId,
            });

        var result = await _service.LoginAsync("dev@test.com", "mypass", null);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(expectedClaim, token.Claims.Single(c => c.Type == WalosClaimTypes.PlatformAdmin).Value);
        Assert.Equal(expectedUserInfo, result.User.IsPlatformAdmin);
    }

    // ── RefreshTokenAsync ──

    [Fact]
    public async Task Refresh_ThrowsValidation_WhenTokenEmpty()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RefreshTokenAsync(""));
    }

    [Fact]
    public async Task Refresh_ThrowsBusiness_WhenTokenInvalid()
    {
        _repoMock.Setup(r => r.GetUserByRefreshTokenAsync("bad-token")).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RefreshTokenAsync("bad-token"));
    }

    [Fact]
    public async Task Refresh_ThrowsBusiness_WhenUserInactive()
    {
        _repoMock.Setup(r => r.GetUserByRefreshTokenAsync("good-token"))
            .ReturnsAsync(new User
            {
                Id = 6, Email = "gone@test.com", IsActive = false,
                PasswordHash = "x", CompanyId = 1, FirstName = "X"
            });

        await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RefreshTokenAsync("good-token"));
    }

    [Fact]
    public async Task Refresh_Success_ReturnsNewTokens()
    {
        _repoMock.Setup(r => r.GetUserByRefreshTokenAsync("valid"))
            .ReturnsAsync(new User
            {
                Id = 7, Email = "refresh@test.com", IsActive = true,
                PasswordHash = "x", CompanyId = 1, FirstName = "R", RoleCode = WalosRoles.Manager
            });

        var result = await _service.RefreshTokenAsync("valid");

        Assert.NotEmpty(result.Token);
        Assert.NotEmpty(result.RefreshToken);
        _repoMock.Verify(r => r.SaveRefreshTokenAsync(7, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_Rejects_NonCanonical_Role_Without_Issuing_Tokens()
    {
        _repoMock.Setup(r => r.GetUserByRefreshTokenAsync("legacy"))
            .ReturnsAsync(new User
            {
                Id = 8,
                Email = "legacy@test.com",
                IsActive = true,
                RoleCode = "admin",
            });

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RefreshTokenAsync("legacy"));

        Assert.Equal("unsupported_role", error.Code);
        _repoMock.Verify(r => r.SaveRefreshTokenAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    // ── LogoutAsync ──

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        await _service.LogoutAsync(99);

        _repoMock.Verify(r => r.SaveRefreshTokenAsync(99, string.Empty, It.Is<DateTime>(d => d < DateTime.UtcNow)), Times.Once);
    }
}
