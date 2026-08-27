using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZEstate.Core.Interfaces;

namespace ZEstate.Infrastructure.Services;

// Runs the obligation-generation sweep once at startup (so a demo/dev environment
// doesn't have to wait for a real month boundary) and then once every 24h. Generation
// itself is idempotent (keyed by Fee/Apartment/Period), so a daily cadence safely
// catches the "start of month" case even if the exact day is missed (e.g. redeploys).
public class ObligationGenerationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ObligationGenerationBackgroundService> _logger;

    public ObligationGenerationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ObligationGenerationBackgroundService> logger)
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
                var service = scope.ServiceProvider.GetRequiredService<IObligationGenerationService>();
                await service.GenerateForCurrentPeriodAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled obligation generation run failed.");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
