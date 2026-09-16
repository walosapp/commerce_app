using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Moq;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Security;

public sealed class AccessTokenValidationServiceTests
{
    private const string Secret = "super-secret-key-for-testing-must-be-at-least-32-chars!";
    private readonly Mock<IAuthRepository> _repository = new();
    private readonly AccessTokenValidationService _service;

    public AccessTokenValidationServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = Secret })
            .Build();
        _service = new AccessTokenValidationService(_repository.Object, configuration);
    }

    [Fact]
    public async Task Validate_Accepts_CurrentActiveSecurityState()
    {
        var user = CurrentUser();
        _repository.Setup(r => r.GetUserForAccessValidationAsync(17, 23)).ReturnsAsync(user);

        Assert.True(await _service.ValidateAsync(PrincipalFor(user)));
    }

    [Fact]
    public async Task Validate_Rejects_AccessTokenAfterPasswordHashChanges()
    {
        var issuedFor = CurrentUser();
        var principal = PrincipalFor(issuedFor);
        var current = CurrentUser();
        current.PasswordHash = "new-password-hash";
        _repository.Setup(r => r.GetUserForAccessValidationAsync(17, 23)).ReturnsAsync(current);

        Assert.False(await _service.ValidateAsync(principal));
    }

    [Theory]
    [InlineData("cashier", 8)]
    [InlineData("manager", 9)]
    public async Task Validate_Rejects_StaleRoleOrBranch(string currentRole, long currentBranch)
    {
        var issuedFor = CurrentUser();
        var principal = PrincipalFor(issuedFor);
        var current = CurrentUser();
        current.RoleCode = currentRole;
        current.BranchId = currentBranch;
        _repository.Setup(r => r.GetUserForAccessValidationAsync(17, 23)).ReturnsAsync(current);

        Assert.False(await _service.ValidateAsync(principal));
    }

    [Fact]
    public async Task Validate_FailsClosed_WhenUserOrCompanyIsInactiveOrMissing()
    {
        var user = CurrentUser();
        _repository.Setup(r => r.GetUserForAccessValidationAsync(17, 23)).ReturnsAsync((User?)null);

        Assert.False(await _service.ValidateAsync(PrincipalFor(user)));
    }

    [Fact]
    public void SecurityStamp_CoversTenantUserPasswordRoleBranchAndTrustedTenantState()
    {
        var baseline = CurrentUser();
        var baselineStamp = AccessTokenSecurityStamp.Compute(Secret, baseline);
        var changedUser = CurrentUser();
        changedUser.Id++;
        var changedTenant = CurrentUser();
        changedTenant.CompanyId++;
        var changedPassword = CurrentUser();
        changedPassword.PasswordHash = "different-password-hash";
        var changedRole = CurrentUser();
        changedRole.RoleCode = WalosRoles.Cashier;
        var changedBranch = CurrentUser();
        changedBranch.BranchId = 9;
        var changedTrustedTenant = CurrentUser();
        changedTrustedTenant.CompanyTaxId = WalosSystemIdentity.CompanyTaxId;

        foreach (var variant in new[]
                 {
                     changedUser,
                     changedTenant,
                     changedPassword,
                     changedRole,
                     changedBranch,
                     changedTrustedTenant
                 })
        {
            Assert.NotEqual(baselineStamp, AccessTokenSecurityStamp.Compute(Secret, variant));
        }
        Assert.DoesNotContain(baseline.PasswordHash, baselineStamp, StringComparison.Ordinal);
    }

    private static User CurrentUser() => new()
    {
        Id = 17,
        CompanyId = 23,
        BranchId = 8,
        RoleCode = WalosRoles.Manager,
        PasswordHash = "current-password-hash",
        CompanyTaxId = "TENANT-23",
        IsActive = true
    };

    private static ClaimsPrincipal PrincipalFor(User user)
    {
        var claims = new[]
        {
            new Claim(WalosClaimTypes.UserId, user.Id.ToString()),
            new Claim(WalosClaimTypes.CompanyId, user.CompanyId.ToString()),
            new Claim(WalosClaimTypes.BranchId, user.BranchId?.ToString() ?? string.Empty),
            new Claim(ClaimTypes.Role, user.RoleCode!),
            new Claim(WalosClaimTypes.PlatformAdmin, "false"),
            new Claim(WalosClaimTypes.SecurityStamp, AccessTokenSecurityStamp.Compute(Secret, user))
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
