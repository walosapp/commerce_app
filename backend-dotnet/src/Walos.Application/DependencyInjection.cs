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
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<ICreditService, CreditService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

        return services;
    }
}
