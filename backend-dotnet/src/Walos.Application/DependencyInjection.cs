using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Walos.Application.Services;
using Walos.Application.Validators;

namespace Walos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

        return services;
    }
}
