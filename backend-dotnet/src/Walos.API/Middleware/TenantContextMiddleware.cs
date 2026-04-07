namespace Walos.API.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantIdHeader = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
        var branchIdHeader = context.Request.Headers["X-Branch-ID"].FirstOrDefault();

        if (long.TryParse(tenantIdHeader, out var tenantId))
            context.Items["TenantId"] = tenantId;

        if (long.TryParse(branchIdHeader, out var branchId))
            context.Items["BranchId"] = branchId;

        await _next(context);
    }
}

public static class HttpContextExtensions
{
    public static long? GetTenantId(this HttpContext context)
        => context.Items.TryGetValue("TenantId", out var val) ? val as long? : null;

    public static long? GetBranchId(this HttpContext context)
        => context.Items.TryGetValue("BranchId", out var val) ? val as long? : null;
}
