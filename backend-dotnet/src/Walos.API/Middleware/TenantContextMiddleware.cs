using System.Security.Claims;
using Walos.API.Services;
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
            tenant.IsAuthenticated = true;

            if (long.TryParse(context.User.FindFirst("companyId")?.Value, out var companyId))
                tenant.CompanyId = companyId;

            if (long.TryParse(context.User.FindFirst("userId")?.Value, out var userId))
                tenant.UserId = userId;

            // BranchId: header > JWT claim
            var branchHeader = context.Request.Headers["X-Branch-ID"].FirstOrDefault();
            if (long.TryParse(branchHeader, out var headerBranch))
                tenant.BranchId = headerBranch;
            else if (long.TryParse(context.User.FindFirst("branchId")?.Value, out var claimBranch))
                tenant.BranchId = claimBranch;

            tenant.Role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
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
