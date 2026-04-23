using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities.Platform;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Services;

public class BillingJobService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingJobService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    public BillingJobService(IServiceScopeFactory scopeFactory, ILogger<BillingJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BillingJobService iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunBillingCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ciclo de facturación");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunBillingCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var platformRepo = scope.ServiceProvider.GetRequiredService<IPlatformRepository>();

        var dueSubscriptions = (await platformRepo.GetSubscriptionsDueTodayAsync()).ToList();
        if (dueSubscriptions.Count == 0)
        {
            _logger.LogDebug("BillingJob: sin suscripciones vencidas hoy");
            return;
        }

        _logger.LogInformation("BillingJob: procesando {Count} suscripciones", dueSubscriptions.Count);

        var grouped = dueSubscriptions.GroupBy(s => s.CompanyId);

        foreach (var group in grouped)
        {
            if (ct.IsCancellationRequested) break;

            var companyId = group.Key;
            var subscriptions = group.ToList();

            try
            {
                await GenerateInvoiceForCompanyAsync(platformRepo, companyId, subscriptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando factura para empresa {CompanyId}", companyId);
            }
        }
    }

    private async Task GenerateInvoiceForCompanyAsync(
        IPlatformRepository platformRepo,
        long companyId,
        List<CompanySubscription> subscriptions)
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = subscriptions.Min(s => s.NextBillingDate ?? now);
        var periodEnd = now;

        var items = subscriptions.Select(s => new BillingInvoiceItem
        {
            ServiceCode  = s.ServiceCode,
            Description  = s.ServiceName ?? s.ServiceCode,
            Quantity     = 1,
            UnitPrice    = s.EffectivePrice,
            Subtotal     = s.EffectivePrice,
        }).ToList();

        var subtotal  = items.Sum(i => i.Subtotal);
        var taxRate   = 19m;
        var taxAmount = Math.Round(subtotal * taxRate / 100, 2);
        var total     = subtotal + taxAmount;

        var invoice = new BillingInvoice
        {
            CompanyId     = companyId,
            InvoiceNumber = $"WAL-{now:yyyyMMdd}-{companyId:D4}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            PeriodStart   = periodStart,
            PeriodEnd     = periodEnd,
            Subtotal      = subtotal,
            TaxRate       = taxRate,
            TaxAmount     = taxAmount,
            Total         = total,
            Status        = "draft",
            DueDate       = now.AddDays(15),
        };

        await platformRepo.CreateInvoiceAsync(invoice, items);

        foreach (var sub in subscriptions)
        {
            var nextDate = sub.BillingFrequency == "annual"
                ? (sub.NextBillingDate ?? now).AddYears(1)
                : (sub.NextBillingDate ?? now).AddMonths(1);

            await platformRepo.UpsertSubscriptionAsync(new CompanySubscription
            {
                CompanyId       = sub.CompanyId,
                ServiceCode     = sub.ServiceCode,
                IsActive        = sub.IsActive,
                CustomPrice     = sub.CustomPrice,
                BillingFrequency = sub.BillingFrequency,
                NextBillingDate = nextDate,
                Notes           = sub.Notes,
            });
        }

        _logger.LogInformation(
            "BillingJob: factura {InvoiceNumber} generada para empresa {CompanyId} — total {Total}",
            invoice.InvoiceNumber, companyId, total);
    }
}
