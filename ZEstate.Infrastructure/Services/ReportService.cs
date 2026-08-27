using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using ZEstate.Core.DTOs.Reports;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FinancialSummaryDto> GetSummaryAsync(string userId, DateTime from, DateTime to)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);

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
            .Select(g => new IncomeByApartmentDto { ApartmentNumber = g.Key, Total = g.Sum(p => p.Amount) })
            .OrderBy(x => x.ApartmentNumber)
            .ToList();

        var incomeByFeeType = payments
            .GroupBy(p => p.Obligation.Fee.Type)
            .Select(g => new IncomeByFeeTypeDto { FeeType = (int)g.Key, Total = g.Sum(p => p.Amount) })
            .ToList();

        var expensesByRepair = repairs
            .Select(r => new ExpenseByRepairDto { Id = r.Id, Title = r.Title, Amount = r.ActualCost ?? r.Budget })
            .ToList();

        return new FinancialSummaryDto
        {
            From = from,
            To = to,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            Balance = totalIncome - totalExpense,
            IncomeByApartment = incomeByApartment,
            IncomeByFeeType = incomeByFeeType,
            ExpensesByRepair = expensesByRepair
        };
    }

    public async Task<List<BalanceHistoryEntryDto>> GetBalanceHistoryAsync(string userId, int months)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);

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

        var result = new List<BalanceHistoryEntryDto>();
        for (var i = 0; i < months; i++)
        {
            var periodStart = rangeStart.AddMonths(i);
            var periodEnd = periodStart.AddMonths(1);

            var income = payments.Where(p => p.PaidAt >= periodStart && p.PaidAt < periodEnd).Sum(p => p.Amount);
            var expense = repairs.Where(r => r.CreatedAt >= periodStart && r.CreatedAt < periodEnd).Sum(r => r.Amount);

            result.Add(new BalanceHistoryEntryDto
            {
                Period = periodStart.ToString("yyyy-MM"),
                Income = income,
                Expense = expense,
                Balance = income - expense
            });
        }

        return result;
    }

    public async Task<ReportExportResult> ExportAsync(string userId, DateTime from, DateTime to)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);

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

        return new ReportExportResult(bytes, fileName);
    }

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    // The house manager owns the building directly (Building.ManagerId); a Cashier
    // isn't a manager, so their building is resolved via their apartment membership.
    private async Task<Building> GetManagedBuildingOrThrowAsync(string userId)
    {
        var managed = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == userId);
        if (managed != null)
            return managed;

        var buildingId = await _context.ApartmentUsers
            .Where(au => au.UserId == userId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        var building = buildingId.HasValue
            ? await _context.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId.Value)
            : null;

        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        return building;
    }
}
