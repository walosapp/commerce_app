using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;
using Walos.Infrastructure.Repositories;
using Walos.Infrastructure.Services;

namespace Walos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<ISalesRepository, SalesRepository>();
        services.AddScoped<IFinanceRepository, FinanceRepository>();

        services.AddHttpClient<IAiService, OpenAiService>(client =>
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration["OpenAI:ApiKey"]}");
        });

        return services;
    }
}
