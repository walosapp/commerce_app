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
            new Claim(ClaimTypes.Role, "manager"),
            new Claim(ClaimTypes.Email, "manager@walos.dev"),
            new Claim("platformAdmin", "true"),
        ], authenticationType: "Bearer"));

        var tenant = new TenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, tenant);

        Assert.True(tenant.IsAuthenticated);
        Assert.Equal(10, tenant.CompanyId);
        Assert.Equal(20, tenant.UserId);
        Assert.Equal(30, tenant.BranchId);
        Assert.Equal("manager", tenant.Role);
        Assert.Equal("manager@walos.dev", tenant.Email);
        Assert.True(tenant.IsPlatformAdmin);
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
        Assert.False(tenant.IsPlatformAdmin);
    }

    [Theory]
    [InlineData(null, "20", "manager", "30")]
    [InlineData("0", "20", "manager", "30")]
    [InlineData("10", "bad", "manager", "30")]
    [InlineData("10", "20", "", "30")]
    [InlineData("10", "20", "manager", "bad")]
    public async Task InvokeAsync_Should_Reject_Authenticated_Principal_With_Malformed_Tenant_Claims(
        string? companyId,
        string userId,
        string role,
        string branchId)
    {
        var claims = new List<Claim>
        {
            new("userId", userId),
            new("branchId", branchId),
            new(ClaimTypes.Role, role),
        };
        if (companyId is not null) claims.Add(new Claim("companyId", companyId));

        var nextCalled = false;
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
        };
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new TenantContext());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextCalled);
    }
}
