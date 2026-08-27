using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZEstate.Core.Interfaces;

namespace ZEstate.Infrastructure.Services;

// Runs the overdue sweep once at startup and then every 24h. Idempotent (only ever
// moves Pending/PartiallyPaid -> Overdue for obligations already past their due date),
// so the daily cadence is safe to repeat.
public class ObligationStatusBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ObligationStatusBackgroundService> _logger;

    public ObligationStatusBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ObligationStatusBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IObligationStatusService>();
                await service.MarkOverdueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled overdue sweep failed.");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
