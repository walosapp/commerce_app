using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Walos.API.Middleware;
using Walos.API.Services;

namespace Walos.Tests.Security;

public class TenantContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Should_Use_BranchId_From_Jwt_Claim_Not_Header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Branch-ID"] = "999";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("companyId", "10"),
            new Claim("userId", "20"),
            new Claim("branchId", "30"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Email, "admin@walos.dev"),
        ], authenticationType: "Bearer"));

        var tenant = new TenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, tenant);

        Assert.True(tenant.IsAuthenticated);
        Assert.Equal(10, tenant.CompanyId);
        Assert.Equal(20, tenant.UserId);
        Assert.Equal(30, tenant.BranchId);
        Assert.Equal("admin", tenant.Role);
        Assert.Equal("admin@walos.dev", tenant.Email);
    }

    [Fact]
    public async Task InvokeAsync_Should_Leave_Tenant_Default_When_User_Is_Not_Authenticated()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        var tenant = new TenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, tenant);

        Assert.False(tenant.IsAuthenticated);
        Assert.Equal(0, tenant.CompanyId);
        Assert.Equal(0, tenant.UserId);
        Assert.Null(tenant.BranchId);
        Assert.Equal(string.Empty, tenant.Role);
        Assert.Equal(string.Empty, tenant.Email);
    }
}
