using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Walos.Application.Services;
using Walos.Application.Validators;

namespace Walos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ICompanyFeatureService, CompanyFeatureService>();
        services.AddScoped<IAiCapabilityGuard, AiCapabilityGuard>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<ICreditService, CreditService>();
        services.AddScoped<ICashRegisterService, CashRegisterService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IUsersService, UsersService>();
        services.AddScoped<ISuppliersService, SuppliersService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

        return services;
    }
}
