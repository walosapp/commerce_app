using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Services;

public class AiSessionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiSessionCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private const int InactiveMinutes = 30;

    public AiSessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<AiSessionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI session cleanup service started (interval: {Interval} min, timeout: {Timeout} min)",
            _interval.TotalMinutes, InactiveMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IAiSessionRepository>();
                await repo.CleanupInactiveSessionsAsync(InactiveMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI session cleanup");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
