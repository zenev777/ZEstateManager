// ReportsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = PolicyNames.PaymentsManagement)]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Обобщен финансов отчет за период - приходи (плащания), разходи (ремонти) и салдо,
    // с разбивка по апартамент и по категория (тип такса).
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var payments = await _context.Payments
            .Where(p => p.Obligation.Apartment.BuildingId == building.Id && p.PaidAt >= from && p.PaidAt <= to)
            .Include(p => p.Obligation).ThenInclude(o => o.Apartment)
            .Include(p => p.Obligation).ThenInclude(o => o.Fee)
            .ToListAsync();

        // Repairs represent the building's expenses; no separate outgoing-payment
        // ledger exists yet, so a repair's (actual or planned) cost within the
        // period is treated as the expense line, per the report's scope.
        var repairs = await _context.Repairs
            .Where(r => r.BuildingId == building.Id
                     && r.Status == RepairStatus.Completed
                     && r.CreatedAt >= from && r.CreatedAt <= to)
            .ToListAsync();

        var totalIncome = payments.Sum(p => p.Amount);
        var totalExpense = repairs.Sum(r => r.ActualCost ?? r.Budget);

        var incomeByApartment = payments
            .GroupBy(p => p.Obligation.Apartment.Number)
            .Select(g => new { apartmentNumber = g.Key, total = g.Sum(p => p.Amount) })
            .OrderBy(x => x.apartmentNumber)
            .ToList();

        var incomeByFeeType = payments
            .GroupBy(p => p.Obligation.Fee.Type)
            .Select(g => new { feeType = g.Key, total = g.Sum(p => p.Amount) })
            .ToList();

        var expensesByRepair = repairs
            .Select(r => new { r.Id, r.Title, amount = r.ActualCost ?? r.Budget })
            .ToList();

        return Ok(new
        {
            from,
            to,
            totalIncome,
            totalExpense,
            balance = totalIncome - totalExpense,
            incomeByApartment,
            incomeByFeeType,
            expensesByRepair
        });
    }

    // GET: Салдо по месеци за последните N месеца (за графика)
    [HttpGet("balance-history")]
    public async Task<IActionResult> GetBalanceHistory([FromQuery] int months = 12)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        months = Math.Clamp(months, 1, 36);
        var rangeStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(months - 1));

        var payments = await _context.Payments
            .Where(p => p.Obligation.Apartment.BuildingId == building.Id && p.PaidAt >= rangeStart)
            .Select(p => new { p.Amount, p.PaidAt })
            .ToListAsync();

        var repairs = await _context.Repairs
            .Where(r => r.BuildingId == building.Id && r.Status == RepairStatus.Completed && r.CreatedAt >= rangeStart)
            .Select(r => new { Amount = r.ActualCost ?? r.Budget, r.CreatedAt })
            .ToListAsync();

        var result = new List<object>();
        for (var i = 0; i < months; i++)
        {
            var periodStart = rangeStart.AddMonths(i);
            var periodEnd = periodStart.AddMonths(1);

            var income = payments.Where(p => p.PaidAt >= periodStart && p.PaidAt < periodEnd).Sum(p => p.Amount);
            var expense = repairs.Where(r => r.CreatedAt >= periodStart && r.CreatedAt < periodEnd).Sum(r => r.Amount);

            result.Add(new
            {
                period = periodStart.ToString("yyyy-MM"),
                income,
                expense,
                balance = income - expense
            });
        }

        return Ok(result);
    }

    // GET: Износ на отчета за период като CSV (отваря се директно в Excel)
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var payments = await _context.Payments
            .Where(p => p.Obligation.Apartment.BuildingId == building.Id && p.PaidAt >= from && p.PaidAt <= to)
            .Include(p => p.Obligation).ThenInclude(o => o.Apartment)
            .Include(p => p.Obligation).ThenInclude(o => o.Fee)
            .OrderBy(p => p.PaidAt)
            .ToListAsync();

        var repairs = await _context.Repairs
            .Where(r => r.BuildingId == building.Id && r.Status == RepairStatus.Completed && r.CreatedAt >= from && r.CreatedAt <= to)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Тип,Дата,Апартамент,Описание,Сума");

        foreach (var payment in payments)
        {
            csv.AppendLine(string.Join(",",
                "Приход",
                payment.PaidAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                payment.Obligation.Apartment.Number,
                CsvEscape(payment.Obligation.Fee.Title),
                payment.Amount.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var repair in repairs)
        {
            csv.AppendLine(string.Join(",",
                "Разход",
                repair.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "-",
                CsvEscape(repair.Title),
                (-(repair.ActualCost ?? repair.Budget)).ToString(CultureInfo.InvariantCulture)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var fileName = $"financial-report_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.csv";

        return File(bytes, "text/csv", fileName);
    }

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // The house manager owns the building directly (Building.ManagerId); a Cashier
    // isn't a manager, so their building is resolved via their apartment membership.
    private async Task<Building?> GetManagedBuildingAsync()
    {
        var managed = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);
        if (managed != null)
            return managed;

        var buildingId = await _context.ApartmentUsers
            .Where(au => au.UserId == CurrentUserId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        return buildingId.HasValue
            ? await _context.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId.Value)
            : null;
    }
}
