using ZEstate.Core.DTOs.Cash;
using ZEstate.Core.Exceptions;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class CashServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CashService _service;
    private const string ManagerId = "mgr1";

    public CashServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new CashService(_context);
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
    public async Task GetBalancesAsync_NoManagedBuilding_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetBalancesAsync("stranger"));
    }

    [Fact]
    public async Task GetBalancesAsync_SumsEntriesPerAccount()
    {
        var building = AddManagedBuilding();
        _context.CashLedgerEntries.AddRange(
            new CashLedgerEntry { BuildingId = building.Id, Account = CashAccountType.Cash, Amount = 30, Description = "d" },
            new CashLedgerEntry { BuildingId = building.Id, Account = CashAccountType.Cash, Amount = 20, Description = "d" },
            new CashLedgerEntry { BuildingId = building.Id, Account = CashAccountType.Bank, Amount = 100, Description = "d" });
        await _context.SaveChangesAsync();

        var result = await _service.GetBalancesAsync(ManagerId);

        Assert.Equal(50, result.CashBalance);
        Assert.Equal(100, result.BankBalance);
    }

    [Fact]
    public async Task TransferAsync_CashToBank_MovesAmountBetweenAccounts()
    {
        var building = AddManagedBuilding();
        _context.CashLedgerEntries.Add(new CashLedgerEntry { BuildingId = building.Id, Account = CashAccountType.Cash, Amount = 100, Description = "d" });
        await _context.SaveChangesAsync();

        await _service.TransferAsync(ManagerId, new TransferFundsDto { From = "Cash", Amount = 40, Note = "внесени в банката" });

        var balances = await _service.GetBalancesAsync(ManagerId);
        Assert.Equal(60, balances.CashBalance);
        Assert.Equal(40, balances.BankBalance);

        var entries = _context.CashLedgerEntries.Where(e => e.TransferGroupId != null).ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal(entries[0].TransferGroupId, e.TransferGroupId));
    }

    [Fact]
    public async Task TransferAsync_InsufficientBalance_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        _context.CashLedgerEntries.Add(new CashLedgerEntry { BuildingId = building.Id, Account = CashAccountType.Cash, Amount = 10, Description = "d" });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.TransferAsync(ManagerId, new TransferFundsDto { From = "Cash", Amount = 50 }));
    }

    [Fact]
    public async Task TransferAsync_InvalidFromAccount_ThrowsBadRequest()
    {
        AddManagedBuilding();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.TransferAsync(ManagerId, new TransferFundsDto { From = "Wallet", Amount = 10 }));
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsNewestFirst()
    {
        var building = AddManagedBuilding();
        _context.CashLedgerEntries.AddRange(
            new CashLedgerEntry { BuildingId = building.Id, Account = CashAccountType.Cash, Amount = 10, Description = "old", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new CashLedgerEntry { BuildingId = building.Id, Account = CashAccountType.Cash, Amount = 20, Description = "new", CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await _service.GetHistoryAsync(ManagerId);

        Assert.Equal(2, result.Count);
        Assert.Equal("new", result[0].Description);
    }
}
