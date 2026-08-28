using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class ObligationGenerationService : IObligationGenerationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ObligationGenerationService> _logger;
    private readonly INotificationService _notificationService;

    public ObligationGenerationService(
        ApplicationDbContext context, ILogger<ObligationGenerationService> logger, INotificationService notificationService)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<ObligationGenerationResult> GenerateForCurrentPeriodAsync()
    {
        var today = DateTime.UtcNow.Date;
        var currentPeriod = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Repair-linked fees are generated directly by the repair-cost workflow, not this job.
        var activeFees = await _context.Fees
            .Where(f => f.Type != FeeType.Repair)
            .Where(f => f.DateFrom <= today && (f.DateTo == null || f.DateTo >= today))
            .ToListAsync();

        var created = 0;
        var skipped = 0;
        var newObligations = new List<(int ApartmentId, string FeeTitle, decimal Amount, DateTime? DueDate)>();

        foreach (var fee in activeFees)
        {
            var apartments = await _context.Apartments
                .Where(a => a.BuildingId == fee.BuildingId)
                .ToListAsync();

            var period = fee.Frequency == FeeFrequency.Monthly ? currentPeriod : (DateTime?)null;

            foreach (var apartment in apartments)
            {
                var alreadyExists = await _context.Obligations.AnyAsync(o =>
                    o.FeeId == fee.Id &&
                    o.ApartmentId == apartment.Id &&
                    o.Period == period);

                if (alreadyExists)
                {
                    skipped++;
                    continue;
                }

                var amount = fee.Type == FeeType.PerIdealPart
                    ? Math.Round(fee.Amount * apartment.IdealParts / 100m, 2)
                    : fee.Amount;

                // Monthly: due by the last day of the period month. OneTime: a two-week grace period.
                var dueDate = period.HasValue
                    ? period.Value.AddMonths(1).AddDays(-1)
                    : fee.DateFrom.AddDays(14);

                _context.Obligations.Add(new Obligation
                {
                    ApartmentId = apartment.Id,
                    FeeId = fee.Id,
                    Amount = amount,
                    Status = ObligationStatus.Pending,
                    DueDate = dueDate,
                    Period = period
                });

                newObligations.Add((apartment.Id, fee.Title, amount, dueDate));
                created++;
            }
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync();
            await NotifyResidentsAsync(newObligations);
        }

        _logger.LogInformation(
            "Obligation generation run for period {Period:yyyy-MM}: {Created} created, {Skipped} already existed.",
            currentPeriod, created, skipped);

        return new ObligationGenerationResult(created, skipped);
    }

    // Notifies every active resident of each apartment a new obligation was just
    // generated for - one query for all involved apartments' active memberships,
    // rather than per-obligation.
    private async Task NotifyResidentsAsync(List<(int ApartmentId, string FeeTitle, decimal Amount, DateTime? DueDate)> newObligations)
    {
        var apartmentIds = newObligations.Select(o => o.ApartmentId).Distinct().ToList();
        var usersByApartment = await _context.ApartmentUsers
            .Where(au => apartmentIds.Contains(au.ApartmentId) && au.IsActive)
            .ToListAsync();

        var usersLookup = usersByApartment
            .GroupBy(au => au.ApartmentId)
            .ToDictionary(g => g.Key, g => g.Select(au => au.UserId).ToList());

        foreach (var obligation in newObligations)
        {
            if (!usersLookup.TryGetValue(obligation.ApartmentId, out var userIds))
                continue;

            var dueText = obligation.DueDate.HasValue ? $", до {obligation.DueDate.Value:dd.MM.yyyy}" : "";
            var message = $"Ново задължение: {obligation.FeeTitle}, {obligation.Amount:0.00} €{dueText}.";

            foreach (var userId in userIds)
            {
                await _notificationService.NotifyAsync(userId, "Ново задължение", message, "/dashboard/fees");
            }
        }
    }
}
