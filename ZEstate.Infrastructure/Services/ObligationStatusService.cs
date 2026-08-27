using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;

namespace ZEstate.Infrastructure.Services;

public class ObligationStatusService : IObligationStatusService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ObligationStatusService> _logger;

    public ObligationStatusService(
        ApplicationDbContext context,
        INotificationService notificationService,
        ILogger<ObligationStatusService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<int> MarkOverdueAsync()
    {
        var today = DateTime.UtcNow.Date;

        var overdue = await _context.Obligations
            .Where(o => (o.Status == ObligationStatus.Pending || o.Status == ObligationStatus.PartiallyPaid)
                     && o.DueDate != null && o.DueDate < today)
            .Include(o => o.Apartment).ThenInclude(a => a.ApartmentUsers)
            .Include(o => o.Fee)
            .ToListAsync();

        foreach (var obligation in overdue)
        {
            obligation.Status = ObligationStatus.Overdue;
        }

        if (overdue.Count > 0)
            await _context.SaveChangesAsync();

        foreach (var obligation in overdue)
        {
            var recipientUserIds = obligation.Apartment.ApartmentUsers
                .Where(au => au.IsActive)
                .Select(au => au.UserId)
                .Distinct();

            foreach (var userId in recipientUserIds)
            {
                await _notificationService.NotifyAsync(
                    userId,
                    "Просрочено задължение",
                    $"Задължението \"{obligation.Fee.Title}\" за апартамент {obligation.Apartment.Number} е просрочено.",
                    "/dashboard/fees");
            }
        }

        _logger.LogInformation("Overdue sweep: {Count} obligations marked overdue.", overdue.Count);

        return overdue.Count;
    }
}
