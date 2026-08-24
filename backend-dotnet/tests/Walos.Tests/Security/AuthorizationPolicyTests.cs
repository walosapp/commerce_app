using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Walos.API.Authorization;
using Walos.Application.Security;

namespace Walos.Tests.Security;

public class AuthorizationPolicyTests
{
    [Theory]
    [InlineData(WalosPolicies.Settings)]
    [InlineData(WalosPolicies.Users)]
    [InlineData(WalosPolicies.Finance)]
    [InlineData(WalosPolicies.InventoryWrite)]
    public async Task Cashier_Is_Denied_Administrative_Policies(string policy)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(Principal(WalosRoles.Cashier), null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosPolicies.TenantManager)]
    [InlineData(WalosPolicies.Settings)]
    [InlineData(WalosPolicies.Users)]
    [InlineData(WalosPolicies.Finance)]
    [InlineData(WalosPolicies.InventoryWrite)]
    [InlineData(WalosPolicies.SalesOperator)]
    [InlineData(WalosPolicies.CashOperator)]
    [InlineData(WalosPolicies.DeliveryOperator)]
    [InlineData(WalosPolicies.DeliveryManage)]
    public async Task Manager_Is_Authorized_For_Phase0_Manager_Policies(string policy)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(Principal(WalosRoles.Manager), null, policy);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Cashier_Can_Operate_Delivery_But_Cannot_Manage_Cancellations_Or_Returns()
    {
        var authorization = CreateAuthorizationService();
        var cashier = Principal(WalosRoles.Cashier);

        var operatorResult = await authorization.AuthorizeAsync(
            cashier, null, WalosPolicies.DeliveryOperator);
        var manageResult = await authorization.AuthorizeAsync(
            cashier, null, WalosPolicies.DeliveryManage);

        Assert.True(operatorResult.Succeeded);
        Assert.False(manageResult.Succeeded);
    }

    [Fact]
    public async Task PlatformAdmin_Denies_Dev_From_A_Tenant_That_Spoofs_The_Role()
    {
        var authorization = CreateAuthorizationService();
        var principal = Principal(WalosRoles.Dev, platformAdmin: false);

        var result = await authorization.AuthorizeAsync(principal, null, WalosPolicies.PlatformAdmin);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PlatformAdmin_Allows_Trusted_System_Dev_Claim()
    {
        var authorization = CreateAuthorizationService();
        var principal = Principal(WalosRoles.Dev, platformAdmin: true);

        var result = await authorization.AuthorizeAsync(principal, null, WalosPolicies.PlatformAdmin);

        Assert.True(result.Succeeded);
    }

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWalosAuthorization();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal Principal(string role, bool? platformAdmin = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new(WalosClaimTypes.UserId, "1"),
            new(WalosClaimTypes.CompanyId, "1"),
        };
        if (platformAdmin.HasValue)
            claims.Add(new Claim(WalosClaimTypes.PlatformAdmin, platformAdmin.Value.ToString().ToLowerInvariant()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }
}
