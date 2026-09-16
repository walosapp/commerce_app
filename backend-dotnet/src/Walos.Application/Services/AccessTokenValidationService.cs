using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Walos.Application.Security;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public sealed class AccessTokenValidationService : IAccessTokenValidationService
{
    private readonly IAuthRepository _repository;
    private readonly string _jwtSecret;

    public AccessTokenValidationService(IAuthRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret not configured");
    }

    public async Task<bool> ValidateAsync(ClaimsPrincipal principal)
    {
        if (!long.TryParse(principal.FindFirst(WalosClaimTypes.UserId)?.Value, out var userId)
            || !long.TryParse(principal.FindFirst(WalosClaimTypes.CompanyId)?.Value, out var companyId))
            return false;

        var user = await _repository.GetUserForAccessValidationAsync(userId, companyId);
        if (user is null)
            return false;

        var expectedStamp = AccessTokenSecurityStamp.Compute(_jwtSecret, user);
        if (!AccessTokenSecurityStamp.FixedTimeEquals(
                expectedStamp,
                principal.FindFirst(WalosClaimTypes.SecurityStamp)?.Value ?? string.Empty))
            return false;

        var claimedRole = principal.FindFirst(ClaimTypes.Role)?.Value;
        var claimedBranch = principal.FindFirst(WalosClaimTypes.BranchId)?.Value ?? string.Empty;
        var currentBranch = user.BranchId?.ToString() ?? string.Empty;
        var claimedPlatformAdmin = principal.FindFirst(WalosClaimTypes.PlatformAdmin)?.Value;
        var currentPlatformAdmin = IsTrustedPlatformAdmin(user).ToString().ToLowerInvariant();

        return string.Equals(claimedRole, user.RoleCode, StringComparison.Ordinal)
            && string.Equals(claimedBranch, currentBranch, StringComparison.Ordinal)
            && string.Equals(claimedPlatformAdmin, currentPlatformAdmin, StringComparison.Ordinal);
    }

    private static bool IsTrustedPlatformAdmin(Walos.Domain.Entities.User user) =>
        (string.Equals(user.RoleCode, WalosRoles.Dev, StringComparison.OrdinalIgnoreCase)
         || string.Equals(user.RoleCode, WalosRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase))
        && string.Equals(user.CompanyTaxId, WalosSystemIdentity.CompanyTaxId, StringComparison.Ordinal);
}
