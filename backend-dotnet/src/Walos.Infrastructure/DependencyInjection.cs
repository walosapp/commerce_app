using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Walos.Application.Services;
using Walos.Application.Storage;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;
using Walos.Infrastructure.Repositories;
using Walos.Infrastructure.Services;
using Walos.Infrastructure.Storage;

namespace Walos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

        services.AddOptions<SupabaseStorageOptions>()
            .Bind(configuration.GetSection(SupabaseStorageOptions.SectionName))
            .Validate(options =>
                Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "SupabaseStorage:Url debe ser una URL HTTP(S) absoluta")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ServiceRoleKey),
                "SupabaseStorage:ServiceRoleKey es obligatorio")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Bucket),
                "SupabaseStorage:Bucket es obligatorio")
            .Validate(options => System.Text.RegularExpressions.Regex.IsMatch(
                    options.Bucket,
                    @"^[a-z0-9][a-z0-9._-]{0,62}$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant),
                "SupabaseStorage:Bucket contiene caracteres no permitidos")
            .ValidateOnStart();

        services.AddSingleton<IImageValidator, ImageSharpImageValidator>();
        services.AddHttpClient<IFileStorage, SupabaseFileStorage>();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<ISuppliersRepository, SuppliersRepository>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddSingleton<ProductExcelService>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<ISalesRepository, SalesRepository>();
        services.AddScoped<ICheckoutRepository, CheckoutRepository>();
        services.AddScoped<ICreditRepository, CreditRepository>();
        services.AddScoped<ICashRegisterRepository, CashRegisterRepository>();
        services.AddScoped<IOrderPaymentRepository, OrderPaymentRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IFinanceRepository, FinanceRepository>();
        services.AddScoped<IPlatformRepository, PlatformRepository>();
        services.AddScoped<IAiSessionRepository, AiSessionRepository>();
        services.AddScoped<OrchestratorService>();
        services.AddHostedService<BillingJobService>();
        services.AddHostedService<AiSessionCleanupService>();

        services.AddHttpClient<IAiService, OpenAiService>(client =>
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration["OpenAI:ApiKey"]}");
        });

        return services;
    }
}
