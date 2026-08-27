using ZEstate.Core.Exceptions;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class ReportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ReportService _service;
    private const string ManagerId = "mgr1";

    public ReportServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new ReportService(_context);
    }

    public void Dispose() => _context.Dispose();

    private Building AddManagedBuilding()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        return building;
    }

    [Fact]
    public async Task GetSummaryAsync_NoManagedBuilding_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetSummaryAsync("stranger", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesIncomeExpenseAndBalance()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        var fee = new Fee { BuildingId = building.Id, Title = "Maintenance", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        var obligation = new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 100, Status = ObligationStatus.Paid };
        _context.Obligations.Add(obligation);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _context.Payments.Add(new Payment { ObligationId = obligation.Id, Amount = 100, PaidAt = now, Method = PaymentMethod.Manual });
        _context.Repairs.Add(new Repair { BuildingId = building.Id, Title = "Roof", Budget = 200, ActualCost = 40, Status = RepairStatus.Completed, CreatedAt = now });
        await _context.SaveChangesAsync();

        var result = await _service.GetSummaryAsync(ManagerId, now.AddDays(-1), now.AddDays(1));

        Assert.Equal(100, result.TotalIncome);
        Assert.Equal(40, result.TotalExpense);
        Assert.Equal(60, result.Balance);
        Assert.Single(result.IncomeByApartment);
        Assert.Single(result.ExpensesByRepair);
    }

    [Fact]
    public async Task GetSummaryAsync_ExcludesPlannedRepairsFromExpenses()
    {
        var building = AddManagedBuilding();
        var now = DateTime.UtcNow;
        _context.Repairs.Add(new Repair { BuildingId = building.Id, Title = "Planned", Budget = 500, Status = RepairStatus.Planned, CreatedAt = now });
        await _context.SaveChangesAsync();

        var result = await _service.GetSummaryAsync(ManagerId, now.AddDays(-1), now.AddDays(1));

        Assert.Equal(0, result.TotalExpense);
    }

    [Fact]
    public async Task GetBalanceHistoryAsync_ClampsMonthsToRange()
    {
        AddManagedBuilding();

        var tooMany = await _service.GetBalanceHistoryAsync(ManagerId, 1000);
        var tooFew = await _service.GetBalanceHistoryAsync(ManagerId, 0);

        Assert.Equal(36, tooMany.Count);
        Assert.Single(tooFew);
    }

    [Fact]
    public async Task GetBalanceHistoryAsync_BucketsByMonth()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        var fee = new Fee { BuildingId = building.Id, Title = "F", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();
        var obligation = new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 50, Status = ObligationStatus.Paid };
        _context.Obligations.Add(obligation);
        await _context.SaveChangesAsync();

        var thisMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _context.Payments.Add(new Payment { ObligationId = obligation.Id, Amount = 50, PaidAt = thisMonthStart.AddDays(2), Method = PaymentMethod.Manual });
        await _context.SaveChangesAsync();

        var history = await _service.GetBalanceHistoryAsync(ManagerId, 1);

        Assert.Single(history);
        Assert.Equal(50, history[0].Income);
        Assert.Equal(50, history[0].Balance);
    }

    [Fact]
    public async Task ExportAsync_ProducesCsvWithIncomeAndExpenseRows()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "7", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        var fee = new Fee { BuildingId = building.Id, Title = "Cleaning", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();
        var obligation = new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 30, Status = ObligationStatus.Paid };
        _context.Obligations.Add(obligation);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _context.Payments.Add(new Payment { ObligationId = obligation.Id, Amount = 30, PaidAt = now, Method = PaymentMethod.Manual });
        _context.Repairs.Add(new Repair { BuildingId = building.Id, Title = "Elevator", Budget = 500, Status = RepairStatus.Completed, CreatedAt = now });
        await _context.SaveChangesAsync();

        var result = await _service.ExportAsync(ManagerId, now.AddDays(-1), now.AddDays(1));
        var csv = System.Text.Encoding.UTF8.GetString(result.Content);

        Assert.Contains("Приход", csv);
        Assert.Contains("Разход", csv);
        Assert.Contains("Cleaning", csv);
        Assert.Contains("Elevator", csv);
        Assert.EndsWith(".csv", result.FileName);
    }
}
