using System.Security.Claims;
using Walos.API.Services;
using Walos.Application.Security;
using Walos.Domain.Interfaces;

namespace Walos.API.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var tenant = (TenantContext)tenantContext;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
            var companyClaim = context.User.FindFirst(WalosClaimTypes.CompanyId)?.Value;
            var userClaim = context.User.FindFirst(WalosClaimTypes.UserId)?.Value;
            var branchClaim = context.User.FindFirst(WalosClaimTypes.BranchId)?.Value;

            long companyId = 0;
            long userId = 0;
            var validIdentity = long.TryParse(companyClaim, out companyId) && companyId > 0
                && long.TryParse(userClaim, out userId) && userId > 0
                && !string.IsNullOrWhiteSpace(role);
            var validBranch = string.IsNullOrWhiteSpace(branchClaim)
                || (long.TryParse(branchClaim, out var parsedBranch) && parsedBranch > 0);

            if (!validIdentity || !validBranch)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            tenant.IsAuthenticated = true;
            tenant.CompanyId = companyId;
            tenant.UserId = userId;

            // Security rule: branch context comes from JWT claims, not from client headers.
            // Headers are client-controlled and must not be allowed to override authenticated identity.
            if (long.TryParse(branchClaim, out var claimBranch))
                tenant.BranchId = claimBranch;

            tenant.Role = role!;
            tenant.Email = context.User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        }

        await _next(context);
    }
}

public static class HttpContextExtensions
{
    public static long? GetBranchId(this HttpContext context)
    {
        var tenant = context.RequestServices.GetService<ITenantContext>();
        return tenant?.BranchId;
    }
}
