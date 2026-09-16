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
    [InlineData(WalosPolicies.PurchasesRead)]
    [InlineData(WalosPolicies.SuppliersRead)]
    [InlineData(WalosPolicies.InventoryRead)]
    [InlineData(WalosPolicies.InventoryWrite)]
    [InlineData(WalosPolicies.Recipes)]
    public async Task Cashier_Is_Denied_Administrative_Policies(string policy)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(Principal(WalosRoles.Cashier), null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosRoles.Cashier, WalosPolicies.PurchasesRead)]
    [InlineData(WalosRoles.Cashier, WalosPolicies.SuppliersRead)]
    [InlineData(WalosRoles.Waiter, WalosPolicies.PurchasesRead)]
    [InlineData(WalosRoles.Waiter, WalosPolicies.SuppliersRead)]
    public async Task NonManagement_Operational_Roles_Are_Denied_Procurement_Reads(
        string role, string policy)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(role), null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosRoles.SuperAdmin, WalosPolicies.PurchasesRead)]
    [InlineData(WalosRoles.SuperAdmin, WalosPolicies.SuppliersRead)]
    [InlineData(WalosRoles.Manager, WalosPolicies.PurchasesRead)]
    [InlineData(WalosRoles.Manager, WalosPolicies.SuppliersRead)]
    public async Task Management_Roles_Are_Authorized_For_Procurement_Reads(
        string role, string policy)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(role), null, policy);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosRoles.SuperAdmin)]
    [InlineData(WalosRoles.Manager)]
    public async Task Dashboard_Allows_Management_Roles(string role)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(role), null, WalosPolicies.Dashboard);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosRoles.Cashier)]
    [InlineData(WalosRoles.Waiter)]
    public async Task Dashboard_Denies_NonManagement_Operational_Roles(string role)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(role), null, WalosPolicies.Dashboard);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Dashboard_Denies_Platform_Admin_As_Non_Operational()
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(WalosRoles.PlatformAdmin, platformAdmin: true), null, WalosPolicies.Dashboard);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosPolicies.TenantManager)]
    [InlineData(WalosPolicies.Settings)]
    [InlineData(WalosPolicies.Users)]
    [InlineData(WalosPolicies.Finance)]
    [InlineData(WalosPolicies.PurchasesRead)]
    [InlineData(WalosPolicies.SuppliersRead)]
    [InlineData(WalosPolicies.InventoryRead)]
    [InlineData(WalosPolicies.InventoryWrite)]
    [InlineData(WalosPolicies.Recipes)]
    [InlineData(WalosPolicies.CatalogRead)]
    [InlineData(WalosPolicies.SalesTableOperator)]
    [InlineData(WalosPolicies.SalesInvoiceOperator)]
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
    public async Task Cashier_Can_Operate_And_Manage_Delivery()
    {
        var authorization = CreateAuthorizationService();
        var cashier = Principal(WalosRoles.Cashier);

        var operatorResult = await authorization.AuthorizeAsync(
            cashier, null, WalosPolicies.DeliveryOperator);
        var manageResult = await authorization.AuthorizeAsync(
            cashier, null, WalosPolicies.DeliveryManage);

        Assert.True(operatorResult.Succeeded);
        Assert.True(manageResult.Succeeded);
    }

    [Theory]
    [InlineData(WalosRoles.Dev)]
    [InlineData(WalosRoles.PlatformAdmin)]
    public async Task PlatformAdmin_Denies_Platform_Role_From_Untrusted_Tenant(string role)
    {
        var authorization = CreateAuthorizationService();
        var principal = Principal(role, platformAdmin: false);

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

    [Theory]
    [InlineData(WalosRoles.Dev)]
    [InlineData(WalosRoles.PlatformAdmin)]
    public async Task PlatformAdmin_Allows_Trusted_System_Platform_Roles(string role)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            Principal(role, platformAdmin: true), null, WalosPolicies.PlatformAdmin);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosPolicies.TenantManager)]
    [InlineData(WalosPolicies.Settings)]
    [InlineData(WalosPolicies.Users)]
    [InlineData(WalosPolicies.Finance)]
    [InlineData(WalosPolicies.PurchasesRead)]
    [InlineData(WalosPolicies.SuppliersRead)]
    [InlineData(WalosPolicies.InventoryRead)]
    [InlineData(WalosPolicies.InventoryWrite)]
    [InlineData(WalosPolicies.Recipes)]
    [InlineData(WalosPolicies.CatalogRead)]
    [InlineData(WalosPolicies.SalesTableOperator)]
    [InlineData(WalosPolicies.SalesInvoiceOperator)]
    [InlineData(WalosPolicies.CashOperator)]
    [InlineData(WalosPolicies.DeliveryOperator)]
    [InlineData(WalosPolicies.DeliveryManage)]
    [InlineData(WalosPolicies.CatalogWrite)]
    [InlineData(WalosPolicies.CatalogDelete)]
    [InlineData(WalosPolicies.PosDeliOperator)]
    public async Task Trusted_System_Dev_Is_Authorized_For_Operational_Policies(string policy)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            Principal(WalosRoles.Dev, platformAdmin: true), null, policy);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosPolicies.TenantManager)]
    [InlineData(WalosPolicies.Settings)]
    [InlineData(WalosPolicies.Users)]
    [InlineData(WalosPolicies.Finance)]
    [InlineData(WalosPolicies.PurchasesRead)]
    [InlineData(WalosPolicies.SuppliersRead)]
    [InlineData(WalosPolicies.InventoryRead)]
    [InlineData(WalosPolicies.InventoryWrite)]
    [InlineData(WalosPolicies.Recipes)]
    [InlineData(WalosPolicies.CatalogRead)]
    [InlineData(WalosPolicies.SalesTableOperator)]
    [InlineData(WalosPolicies.SalesInvoiceOperator)]
    [InlineData(WalosPolicies.CashOperator)]
    [InlineData(WalosPolicies.DeliveryOperator)]
    [InlineData(WalosPolicies.DeliveryManage)]
    [InlineData(WalosPolicies.CatalogWrite)]
    [InlineData(WalosPolicies.CatalogDelete)]
    [InlineData(WalosPolicies.PosDeliOperator)]
    public async Task Untrusted_Tenant_Dev_Is_Denied_Operational_Policies(string policy)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            Principal(WalosRoles.Dev, platformAdmin: false), null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosPolicies.TenantManager)]
    [InlineData(WalosPolicies.Settings)]
    [InlineData(WalosPolicies.Users)]
    [InlineData(WalosPolicies.Finance)]
    [InlineData(WalosPolicies.PurchasesRead)]
    [InlineData(WalosPolicies.SuppliersRead)]
    [InlineData(WalosPolicies.InventoryRead)]
    [InlineData(WalosPolicies.InventoryWrite)]
    [InlineData(WalosPolicies.Recipes)]
    [InlineData(WalosPolicies.CatalogRead)]
    [InlineData(WalosPolicies.SalesTableOperator)]
    [InlineData(WalosPolicies.SalesInvoiceOperator)]
    [InlineData(WalosPolicies.CashOperator)]
    [InlineData(WalosPolicies.DeliveryOperator)]
    [InlineData(WalosPolicies.DeliveryManage)]
    [InlineData(WalosPolicies.CatalogWrite)]
    [InlineData(WalosPolicies.CatalogDelete)]
    [InlineData(WalosPolicies.PosDeliOperator)]
    public async Task PlatformAdmin_Is_Never_Authorized_For_Operational_Policies(string policy)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            Principal(WalosRoles.PlatformAdmin, platformAdmin: true), null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosPolicies.Dashboard)]
    [InlineData(WalosPolicies.TenantManager)]
    [InlineData(WalosPolicies.Settings)]
    [InlineData(WalosPolicies.Users)]
    [InlineData(WalosPolicies.Finance)]
    [InlineData(WalosPolicies.PurchasesRead)]
    [InlineData(WalosPolicies.SuppliersRead)]
    [InlineData(WalosPolicies.InventoryRead)]
    [InlineData(WalosPolicies.InventoryWrite)]
    [InlineData(WalosPolicies.Recipes)]
    [InlineData(WalosPolicies.CatalogRead)]
    [InlineData(WalosPolicies.SalesTableOperator)]
    [InlineData(WalosPolicies.SalesInvoiceOperator)]
    [InlineData(WalosPolicies.CashOperator)]
    [InlineData(WalosPolicies.DeliveryOperator)]
    [InlineData(WalosPolicies.DeliveryManage)]
    [InlineData(WalosPolicies.CatalogWrite)]
    [InlineData(WalosPolicies.CatalogDelete)]
    [InlineData(WalosPolicies.PosDeliOperator)]
    public async Task Legacy_Admin_Is_Denied_Operational_Policies(string policy)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal("admin"), null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("admin", false)]
    [InlineData("owner", false)]
    [InlineData(WalosRoles.PlatformAdmin, true)]
    [InlineData(WalosRoles.Dev, false)]
    public async Task DefaultPolicy_Denies_Legacy_Unknown_And_PlatformOnly_Principals(
        string role, bool platformAdmin)
    {
        using var services = CreateServices();
        var policy = await services.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetDefaultPolicyAsync();
        var result = await services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(Principal(role, platformAdmin), null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosRoles.SuperAdmin, false)]
    [InlineData(WalosRoles.Manager, false)]
    [InlineData(WalosRoles.Cashier, false)]
    [InlineData(WalosRoles.Waiter, false)]
    [InlineData(WalosRoles.Dev, true)]
    public async Task DefaultPolicy_Allows_Tenant_Roles_And_Trusted_Dev(
        string role, bool platformAdmin)
    {
        using var services = CreateServices();
        var policy = await services.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetDefaultPolicyAsync();
        var result = await services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(Principal(role, platformAdmin), null, policy);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(WalosRoles.SuperAdmin, false)]
    [InlineData(WalosRoles.Manager, false)]
    [InlineData(WalosRoles.Cashier, false)]
    [InlineData(WalosRoles.Waiter, false)]
    [InlineData(WalosRoles.PlatformAdmin, false)]
    [InlineData(WalosRoles.PlatformAdmin, true)]
    [InlineData(WalosRoles.Dev, true)]
    public async Task CanonicalAuthenticated_Allows_Canonical_Logout_Principals(
        string role, bool platformAdmin)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(role, platformAdmin), null, WalosPolicies.CanonicalAuthenticated);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("admin", false)]
    [InlineData("owner", false)]
    [InlineData(WalosRoles.Dev, false)]
    public async Task CanonicalAuthenticated_Denies_Legacy_Unknown_And_RawDev(
        string role, bool platformAdmin)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(role, platformAdmin), null, WalosPolicies.CanonicalAuthenticated);

        Assert.False(result.Succeeded);
    }

    private static IAuthorizationService CreateAuthorizationService()
        => CreateServices().GetRequiredService<IAuthorizationService>();

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWalosAuthorization();
        return services.BuildServiceProvider();
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
