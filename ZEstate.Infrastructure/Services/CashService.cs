using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Cash;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class CashService : ICashService
{
    private readonly ApplicationDbContext _context;

    public CashService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CashBalancesDto> GetBalancesAsync(string userId)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);
        return await ComputeBalancesAsync(building.Id);
    }

    // Records an internal transfer as two linked ledger entries (one negative leg on
    // the source account, one positive leg on the destination) rather than mutating
    // a stored balance directly, so the full movement history stays auditable.
    public async Task TransferAsync(string userId, TransferFundsDto dto)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);

        if (!Enum.TryParse<CashAccountType>(dto.From, true, out var fromAccount))
            throw new BadRequestException("Невалидна каса. Позволени стойности: Cash, Bank.");

        var toAccount = fromAccount == CashAccountType.Cash ? CashAccountType.Bank : CashAccountType.Cash;

        var balances = await ComputeBalancesAsync(building.Id);
        var fromBalance = fromAccount == CashAccountType.Cash ? balances.CashBalance : balances.BankBalance;
        if (dto.Amount > fromBalance)
            throw new BadRequestException("Няма достатъчно средства в избраната каса.");

        var groupId = Guid.NewGuid();
        var fromLabel = AccountLabel(fromAccount);
        var toLabel = AccountLabel(toAccount);
        var noteSuffix = string.IsNullOrWhiteSpace(dto.Note) ? "" : $" — {dto.Note.Trim()}";

        _context.CashLedgerEntries.AddRange(
            new CashLedgerEntry
            {
                BuildingId = building.Id,
                Account = fromAccount,
                Amount = -dto.Amount,
                Description = $"Прехвърляне към {toLabel}{noteSuffix}",
                TransferGroupId = groupId,
                CreatedByUserId = userId
            },
            new CashLedgerEntry
            {
                BuildingId = building.Id,
                Account = toAccount,
                Amount = dto.Amount,
                Description = $"Прехвърляне от {fromLabel}{noteSuffix}",
                TransferGroupId = groupId,
                CreatedByUserId = userId
            });

        await _context.SaveChangesAsync();
    }

    public async Task WithdrawForRepairAsync(string userId, WithdrawForRepairDto dto)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);

        if (!Enum.TryParse<CashAccountType>(dto.Account, true, out var account))
            throw new BadRequestException("Невалидна каса. Позволени стойности: Cash, Bank.");

        var repair = await _context.Repairs.FirstOrDefaultAsync(r => r.Id == dto.RepairId && r.BuildingId == building.Id);
        if (repair == null)
            throw new NotFoundException("Ремонтът не е намерен.");

        var balances = await ComputeBalancesAsync(building.Id);
        var balance = account == CashAccountType.Cash ? balances.CashBalance : balances.BankBalance;
        if (dto.Amount > balance)
            throw new BadRequestException("Няма достатъчно средства в избраната каса.");

        var noteSuffix = string.IsNullOrWhiteSpace(dto.Note) ? "" : $" — {dto.Note.Trim()}";

        _context.CashLedgerEntries.Add(new CashLedgerEntry
        {
            BuildingId = building.Id,
            Account = account,
            Amount = -dto.Amount,
            Description = $"Теглене за ремонт: {repair.Title}{noteSuffix}",
            RepairId = repair.Id,
            CreatedByUserId = userId
        });

        repair.ActualCost = (repair.ActualCost ?? 0) + dto.Amount;

        await _context.SaveChangesAsync();
    }

    public async Task<List<CashLedgerEntryDto>> GetHistoryAsync(string userId)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);

        return await _context.CashLedgerEntries
            .Where(e => e.BuildingId == building.Id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new CashLedgerEntryDto
            {
                Id = e.Id,
                Account = (int)e.Account,
                Amount = e.Amount,
                Description = e.Description,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();
    }

    private async Task<CashBalancesDto> ComputeBalancesAsync(int buildingId)
    {
        var entries = await _context.CashLedgerEntries
            .Where(e => e.BuildingId == buildingId)
            .Select(e => new { e.Account, e.Amount })
            .ToListAsync();

        return new CashBalancesDto
        {
            CashBalance = entries.Where(e => e.Account == CashAccountType.Cash).Sum(e => e.Amount),
            BankBalance = entries.Where(e => e.Account == CashAccountType.Bank).Sum(e => e.Amount)
        };
    }

    private static string AccountLabel(CashAccountType account) => account == CashAccountType.Cash ? "брой" : "банка";

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
