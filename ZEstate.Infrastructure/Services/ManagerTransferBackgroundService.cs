using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZEstate.Core.Interfaces;

namespace ZEstate.Infrastructure.Services;

// Checks every hour for manager transfers whose grace period has elapsed and applies
// them. Hourly (not daily, like the fee/obligation jobs) because a 2-day grace window
// deserves same-hour precision rather than being off by up to a day.
public class ManagerTransferBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ManagerTransferBackgroundService> _logger;

    public ManagerTransferBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ManagerTransferBackgroundService> logger)
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
                var service = scope.ServiceProvider.GetRequiredService<IManagerTransferService>();
                await service.ApplyDueTransfersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled manager-transfer sweep failed.");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
