using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Walos.Application.Security;

namespace Walos.API.Authorization;

public static class WalosAuthorizationExtensions
{
    public static IServiceCollection AddWalosAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, WalosAuthorizationResultHandler>();
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(context => IsOperationalPrincipal(context.User))
                .Build();

            options.AddPolicy(WalosPolicies.CanonicalAuthenticated, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    IsOperationalPrincipal(context.User)
                    || context.User.IsInRole(WalosRoles.PlatformAdmin));
            });

            AddRolePolicy(options, WalosPolicies.TenantManager,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.Settings,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.Users,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.Dashboard,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.Finance,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.PurchasesRead,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.SuppliersRead,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.InventoryRead,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.InventoryWrite,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.Recipes,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.CatalogRead,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Waiter);
            AddRolePolicy(options, WalosPolicies.SalesTableOperator,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Waiter);
            AddRolePolicy(options, WalosPolicies.SalesInvoiceOperator,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier);
            AddRolePolicy(options, WalosPolicies.CashOperator,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier);
            AddRolePolicy(options, WalosPolicies.DeliveryOperator,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier, WalosRoles.Waiter);
            AddRolePolicy(options, WalosPolicies.DeliveryManage,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier);
            AddRolePolicy(options, WalosPolicies.CatalogWrite,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.CatalogDelete,
                WalosRoles.SuperAdmin, WalosRoles.Manager);
            AddRolePolicy(options, WalosPolicies.PosDeliOperator,
                WalosRoles.SuperAdmin, WalosRoles.Manager, WalosRoles.Cashier);

            options.AddPolicy(WalosPolicies.PlatformAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    IsTrustedPlatformPrincipal(context.User)
                    && (context.User.IsInRole(WalosRoles.Dev)
                        || context.User.IsInRole(WalosRoles.PlatformAdmin)));
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
            policy.RequireAssertion(context =>
                roles.Any(context.User.IsInRole)
                || IsTrustedDev(context.User));
        });
    }

    private static bool IsTrustedDev(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(WalosRoles.Dev) && IsTrustedPlatformPrincipal(user);

    private static bool IsOperationalPrincipal(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(WalosRoles.SuperAdmin)
        || user.IsInRole(WalosRoles.Manager)
        || user.IsInRole(WalosRoles.Cashier)
        || user.IsInRole(WalosRoles.Waiter)
        || IsTrustedDev(user);

    private static bool IsTrustedPlatformPrincipal(System.Security.Claims.ClaimsPrincipal user) =>
        user.HasClaim(WalosClaimTypes.PlatformAdmin, bool.TrueString.ToLowerInvariant());
}
