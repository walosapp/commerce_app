using Microsoft.AspNetCore.Authorization;
using Walos.Application.Security;

namespace Walos.API.Authorization;

public static class WalosAuthorizationExtensions
{
    public static IServiceCollection AddWalosAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            AddRolePolicy(options, WalosPolicies.TenantManager,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.Settings,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.Users,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.Finance,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.InventoryWrite,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.SalesOperator,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Waiter);
            AddRolePolicy(options, WalosPolicies.CashOperator,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier);
            AddRolePolicy(options, WalosPolicies.DeliveryOperator,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Waiter);
            AddRolePolicy(options, WalosPolicies.DeliveryManage,
                WalosRoles.SuperAdmin, WalosRoles.Manager);

            options.AddPolicy(WalosPolicies.PlatformAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(WalosRoles.Dev);
                policy.RequireClaim(WalosClaimTypes.PlatformAdmin, bool.TrueString.ToLowerInvariant());
            });
        });

        return services;
    }

    private static void AddRolePolicy(
        AuthorizationOptions options,
        string name,
        params string[] roles)
    {
        options.AddPolicy(name, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(roles);
        });
    }
}
