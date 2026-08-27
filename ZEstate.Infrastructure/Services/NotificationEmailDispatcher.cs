using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZEstate.Core.Interfaces;

namespace ZEstate.Infrastructure.Services;

// Background worker that drains NotificationEmailQueue and sends each job through
// IEmailSender, so queuing a notification never blocks the HTTP request that raised it.
public class NotificationEmailDispatcher : BackgroundService
{
    private readonly NotificationEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationEmailDispatcher> _logger;

    public NotificationEmailDispatcher(
        NotificationEmailQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationEmailDispatcher> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await emailSender.SendAsync(job.ToEmail, job.Subject, job.HtmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send queued notification email to {Email}", job.ToEmail);
            }
        }
    }
}
