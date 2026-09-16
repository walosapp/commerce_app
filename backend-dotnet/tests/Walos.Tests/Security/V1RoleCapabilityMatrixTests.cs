using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Walos.API.Authorization;
using Walos.Application.Security;

namespace Walos.Tests.Security;

public class V1RoleCapabilityMatrixTests
{
    private static readonly string[] TenantRoles =
    [
        WalosRoles.SuperAdmin,
        WalosRoles.Manager,
        WalosRoles.Cashier,
        WalosRoles.Waiter,
        WalosRoles.Dev,
        WalosRoles.PlatformAdmin
    ];

    private static readonly IReadOnlyDictionary<string, string[]> AllowedRolesByCapability =
        new Dictionary<string, string[]>
        {
            [WalosPolicies.TenantManager] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.Dashboard] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.InventoryRead] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.InventoryWrite] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.Recipes] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.PurchasesRead] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.SuppliersRead] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.Finance] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.Users] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.Settings] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.CatalogRead] =
                [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Waiter, WalosRoles.Dev],
            [WalosPolicies.CatalogWrite] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.CatalogDelete] = [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Dev],
            [WalosPolicies.SalesTableOperator] =
                [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Waiter, WalosRoles.Dev],
            [WalosPolicies.SalesInvoiceOperator] =
                [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Dev],
            [WalosPolicies.PosDeliOperator] =
                [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Dev],
            [WalosPolicies.CashOperator] =
                [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Dev],
            [WalosPolicies.DeliveryOperator] =
                [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Waiter, WalosRoles.Dev],
            [WalosPolicies.DeliveryManage] =
                [WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Dev],
            [WalosPolicies.PlatformAdmin] = [WalosRoles.Dev, WalosRoles.PlatformAdmin]
        };

    public static IEnumerable<object[]> Matrix()
    {
        foreach (var (capability, allowedRoles) in AllowedRolesByCapability)
        foreach (var role in TenantRoles)
            yield return [role, capability, allowedRoles.Contains(role, StringComparer.Ordinal), IsTrustedPlatformRole(role)];
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Canonical_Role_Capability_Matrix_Is_Enforced(
        string role,
        string capability,
        bool expected,
        bool platformAdmin)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(role, platformAdmin), null, capability);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(WalosRoles.Waiter, WalosPolicies.Finance, false)]
    [InlineData(WalosRoles.Waiter, WalosPolicies.InventoryRead, false)]
    [InlineData(WalosRoles.Waiter, WalosPolicies.SalesInvoiceOperator, false)]
    [InlineData(WalosRoles.Cashier, WalosPolicies.Dashboard, false)]
    [InlineData(WalosRoles.Cashier, WalosPolicies.InventoryRead, false)]
    [InlineData(WalosRoles.Cashier, WalosPolicies.CashOperator, true)]
    [InlineData(WalosRoles.Cashier, WalosPolicies.DeliveryManage, true)]
    [InlineData(WalosRoles.Waiter, WalosPolicies.DeliveryManage, false)]
    [InlineData(WalosRoles.Manager, WalosPolicies.Users, true)]
    [InlineData(WalosRoles.Manager, WalosPolicies.Settings, true)]
    [InlineData(WalosRoles.PlatformAdmin, WalosPolicies.PosDeliOperator, false)]
    [InlineData(WalosRoles.Dev, WalosPolicies.PosDeliOperator, true)]
    public async Task Named_Direct_Api_Access_Cases_Are_Enforced(
        string role,
        string policy,
        bool expected)
    {
        var result = await CreateAuthorizationService().AuthorizeAsync(
            Principal(role, IsTrustedPlatformRole(role)), null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    private static bool IsTrustedPlatformRole(string role) =>
        role is WalosRoles.Dev or WalosRoles.PlatformAdmin;

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWalosAuthorization();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal Principal(string role, bool platformAdmin)
    {
        Claim[] claims =
        [
            new(ClaimTypes.Role, role),
            new(WalosClaimTypes.UserId, "1"),
            new(WalosClaimTypes.CompanyId, "1"),
            new(WalosClaimTypes.PlatformAdmin, platformAdmin.ToString().ToLowerInvariant())
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }
}
