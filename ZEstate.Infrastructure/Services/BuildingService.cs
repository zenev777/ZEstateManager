using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Buildings;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Core.Validation;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class BuildingService : IBuildingService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public BuildingService(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<BuildingSummaryDto> GetMyBuildingAsync(string managerId) =>
        ToDto(await GetManagedBuildingOrThrowAsync(managerId));

    public async Task<BuildingSummaryDto> UpdateMyBuildingAsync(string managerId, UpdateBuildingDto dto)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        building.Name = dto.Name;
        building.Address = dto.Address;

        await _context.SaveChangesAsync();

        return ToDto(building);
    }

    public async Task<BuildingSummaryDto> UpdateIbanAsync(string managerId, string iban)
    {
        if (!IbanValidator.IsValid(iban))
            throw new BadRequestException("Невалиден IBAN.");

        var building = await GetManagedBuildingOrThrowAsync(managerId);
        building.Iban = IbanValidator.Normalize(iban);

        await _context.SaveChangesAsync();

        return ToDto(building);
    }

    public async Task<BuildingSummaryDto> RegenerateInviteCodeAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);
        var oldCode = building.InviteCode;

        string newCode;
        do
        {
            newCode = GenerateInviteCode();
        } while (await _context.Buildings.AnyAsync(b => b.InviteCode == newCode));

        building.InviteCode = newCode;
        building.InviteCodeActive = true;
        building.InviteCodeUseCount = 0;

        _context.InviteCodeLogs.Add(new InviteCodeLog
        {
            BuildingId = building.Id,
            ChangedByUserId = managerId,
            Action = InviteCodeAction.Regenerated,
            OldCode = oldCode,
            NewCode = newCode
        });

        await _context.SaveChangesAsync();

        return ToDto(building);
    }

    public async Task<BuildingSummaryDto> RevokeInviteCodeAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        building.InviteCodeActive = false;

        _context.InviteCodeLogs.Add(new InviteCodeLog
        {
            BuildingId = building.Id,
            ChangedByUserId = managerId,
            Action = InviteCodeAction.Revoked,
            OldCode = building.InviteCode
        });

        await _context.SaveChangesAsync();

        return ToDto(building);
    }

    public async Task<BuildingSummaryDto> UpdateInviteCodeLimitsAsync(string managerId, InviteCodeLimitsDto dto)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        building.InviteCodeExpiresAt = dto.ExpiresAt;
        building.InviteCodeMaxUses = dto.MaxUses;

        _context.InviteCodeLogs.Add(new InviteCodeLog
        {
            BuildingId = building.Id,
            ChangedByUserId = managerId,
            Action = InviteCodeAction.LimitsUpdated,
            OldCode = building.InviteCode,
            NewCode = building.InviteCode
        });

        await _context.SaveChangesAsync();

        return ToDto(building);
    }

    public async Task<List<InviteCodeLogEntryDto>> GetInviteCodeLogAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var log = await _context.InviteCodeLogs
            .Where(l => l.BuildingId == building.Id)
            .Include(l => l.ChangedBy)
            .OrderByDescending(l => l.ChangedAt)
            .ToListAsync();

        return log.Select(l => new InviteCodeLogEntryDto
        {
            Id = l.Id,
            Action = (int)l.Action,
            OldCode = l.OldCode,
            NewCode = l.NewCode,
            ChangedAt = l.ChangedAt,
            ChangedByName = l.ChangedBy.Name
        }).ToList();
    }

    public async Task<BuildingSummaryDto> UpdateQuorumThresholdAsync(string managerId, decimal quorumThresholdPercent)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        building.QuorumThresholdPercent = quorumThresholdPercent;
        await _context.SaveChangesAsync();

        return ToDto(building);
    }

    public async Task<ApartmentListDto> GetApartmentsAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var apartments = await _context.Apartments
            .Where(a => a.BuildingId == building.Id)
            .OrderBy(a => a.Number)
            .ToListAsync();

        return new ApartmentListDto
        {
            Apartments = apartments.Select(ToDto).ToList(),
            IdealPartsTotal = apartments.Sum(a => a.IdealParts)
        };
    }

    public async Task<ApartmentSummaryDto> CreateApartmentAsync(string managerId, CreateApartmentDto dto)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var numberTaken = await _context.Apartments
            .AnyAsync(a => a.BuildingId == building.Id && a.Number == dto.Number);
        if (numberTaken)
            throw new BadRequestException("Вече има апартамент с този номер.");

        var currentTotal = await _context.Apartments
            .Where(a => a.BuildingId == building.Id)
            .SumAsync(a => a.IdealParts);

        if (currentTotal + dto.IdealParts > 100)
            throw new BadRequestException($"Сборът от идеалните части не може да надвишава 100%. Свободни: {100 - currentTotal}%.");

        var apartment = new Apartment
        {
            BuildingId = building.Id,
            Number = dto.Number,
            Floor = dto.Floor,
            IdealParts = dto.IdealParts,
            Budget = 0
        };

        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        return ToDto(apartment);
    }

    public async Task<ApartmentSummaryDto> UpdateApartmentAsync(string managerId, int apartmentId, UpdateApartmentDto dto)
    {
        var apartment = await GetOwnedApartmentOrThrowAsync(managerId, apartmentId);

        var numberTaken = await _context.Apartments
            .AnyAsync(a => a.BuildingId == apartment.BuildingId && a.Number == dto.Number && a.Id != apartmentId);
        if (numberTaken)
            throw new BadRequestException("Вече има апартамент с този номер.");

        var otherTotal = await _context.Apartments
            .Where(a => a.BuildingId == apartment.BuildingId && a.Id != apartmentId)
            .SumAsync(a => a.IdealParts);

        if (otherTotal + dto.IdealParts > 100)
            throw new BadRequestException($"Сборът от идеалните части не може да надвишава 100%. Свободни: {100 - otherTotal}%.");

        apartment.Number = dto.Number;
        apartment.Floor = dto.Floor;
        apartment.IdealParts = dto.IdealParts;

        await _context.SaveChangesAsync();

        return ToDto(apartment);
    }

    public async Task DeleteApartmentAsync(string managerId, int apartmentId)
    {
        var apartment = await GetOwnedApartmentOrThrowAsync(managerId, apartmentId);

        var hasResidents = await _context.ApartmentUsers.AnyAsync(au => au.ApartmentId == apartmentId);
        if (hasResidents)
            throw new BadRequestException("Апартаментът има свързани живущи и не може да бъде изтрит.");

        _context.Apartments.Remove(apartment);
        await _context.SaveChangesAsync();
    }

    public async Task<ApartmentTransferResult> TransferApartmentAsync(string managerId, int apartmentId, string debtHandlingValue)
    {
        if (!Enum.TryParse<DebtHandling>(debtHandlingValue, true, out var debtHandling))
            throw new BadRequestException("Невалидна стойност. Позволени: TransfersToNewOwner, StaysWithPreviousOwner.");

        var apartment = await GetOwnedApartmentOrThrowAsync(managerId, apartmentId);

        var activeMembers = await _context.ApartmentUsers
            .Where(au => au.ApartmentId == apartmentId && au.IsActive)
            .Include(au => au.User)
            .ToListAsync();

        if (activeMembers.Count == 0)
            throw new BadRequestException("Апартаментът няма активен собственик за прехвърляне.");

        var outstandingObligations = await _context.Obligations
            .Where(o => o.ApartmentId == apartmentId
                     && (o.Status == ObligationStatus.Pending || o.Status == ObligationStatus.PartiallyPaid || o.Status == ObligationStatus.Overdue))
            .Include(o => o.Payments)
            .ToListAsync();

        var outstandingBalance = outstandingObligations.Sum(o => o.Amount - o.Payments.Sum(p => p.Amount));

        // With co-owners this picks one as "the" previous owner for the audit log;
        // every active member still loses access below regardless.
        var previousOwner = activeMembers.FirstOrDefault(au => au.Role == ApartmentRole.Owner) ?? activeMembers[0];

        foreach (var member in activeMembers)
        {
            member.IsActive = false;
        }

        if (debtHandling == DebtHandling.StaysWithPreviousOwner)
        {
            foreach (var obligation in outstandingObligations)
            {
                obligation.PreviousOwnerUserId = previousOwner.UserId;
            }
        }

        _context.ApartmentTransferLogs.Add(new ApartmentTransferLog
        {
            ApartmentId = apartmentId,
            PreviousOwnerUserId = previousOwner.UserId,
            TransferredByUserId = managerId,
            DebtHandling = debtHandling,
            OutstandingBalanceAtTransfer = outstandingBalance
        });

        await _context.SaveChangesAsync();

        foreach (var member in activeMembers)
        {
            await _notificationService.NotifyAsync(
                member.UserId,
                "Достъпът ти беше прекратен",
                $"Домоуправителят маркира апартамент {apartment.Number} като прехвърлен. Достъпът ти до сградата е деактивиран.",
                null,
                allowEmail: true);
        }

        return new ApartmentTransferResult(outstandingBalance, debtHandling.ToString());
    }

    public async Task<List<ApartmentTransferRecordDto>> GetApartmentTransfersAsync(string managerId, int apartmentId)
    {
        await GetOwnedApartmentOrThrowAsync(managerId, apartmentId);

        var transfers = await _context.ApartmentTransferLogs
            .Where(t => t.ApartmentId == apartmentId)
            .Include(t => t.PreviousOwner)
            .Include(t => t.TransferredBy)
            .OrderByDescending(t => t.TransferredAt)
            .ToListAsync();

        return transfers.Select(t => new ApartmentTransferRecordDto
        {
            Id = t.Id,
            PreviousOwnerName = t.PreviousOwner?.Name,
            TransferredByName = t.TransferredBy.Name,
            DebtHandling = (int)t.DebtHandling,
            OutstandingBalanceAtTransfer = t.OutstandingBalanceAtTransfer,
            TransferredAt = t.TransferredAt
        }).ToList();
    }

    public async Task<List<JoinRequestSummaryDto>> GetJoinRequestsAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var requests = await _context.JoinRequests
            .Where(jr => jr.BuildingId == building.Id && jr.Status == JoinRequestStatus.Pending)
            .Include(jr => jr.User)
            .Include(jr => jr.Apartment)
            .OrderBy(jr => jr.CreatedAt)
            .ToListAsync();

        return requests.Select(jr => new JoinRequestSummaryDto
        {
            Id = jr.Id,
            Name = jr.User.Name,
            Email = jr.User.Email,
            Phone = jr.User.PhoneNumber,
            ApartmentNumber = jr.Apartment.Number,
            RequestedRole = (int)jr.RequestedRole,
            Notes = jr.Notes,
            CreatedAt = jr.CreatedAt
        }).ToList();
    }

    public async Task ApproveJoinRequestAsync(string managerId, int joinRequestId)
    {
        var joinRequest = await GetPendingJoinRequestOrThrowAsync(managerId, joinRequestId);

        joinRequest.Status = JoinRequestStatus.Approved;
        joinRequest.ReviewedAt = DateTime.UtcNow;

        _context.ApartmentUsers.Add(new ApartmentUser
        {
            ApartmentId = joinRequest.ApartmentId,
            UserId = joinRequest.UserId,
            Role = joinRequest.RequestedRole,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _notificationService.NotifyAsync(
            joinRequest.UserId,
            "Заявката ти е одобрена",
            $"Заявката ти за апартамент {joinRequest.Apartment.Number} в {joinRequest.Building.Name} е одобрена.",
            "/dashboard");
    }

    public async Task RejectJoinRequestAsync(string managerId, int joinRequestId, string? reason)
    {
        var joinRequest = await GetPendingJoinRequestOrThrowAsync(managerId, joinRequestId);

        joinRequest.Status = JoinRequestStatus.Rejected;
        joinRequest.ReviewedAt = DateTime.UtcNow;
        joinRequest.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _context.SaveChangesAsync();

        var message = joinRequest.RejectionReason != null
            ? $"Заявката ти за апартамент {joinRequest.Apartment.Number} в {joinRequest.Building.Name} беше отхвърлена. Причина: {joinRequest.RejectionReason}"
            : $"Заявката ти за апартамент {joinRequest.Apartment.Number} в {joinRequest.Building.Name} беше отхвърлена.";

        await _notificationService.NotifyAsync(joinRequest.UserId, "Заявката ти беше отхвърлена", message, "/dashboard");
    }

    private static BuildingSummaryDto ToDto(Building building) => new()
    {
        Id = building.Id,
        Name = building.Name,
        Address = building.Address,
        InviteCode = building.InviteCode,
        InviteCodeActive = building.InviteCodeActive,
        InviteCodeExpiresAt = building.InviteCodeExpiresAt,
        InviteCodeMaxUses = building.InviteCodeMaxUses,
        InviteCodeUseCount = building.InviteCodeUseCount,
        QuorumThresholdPercent = building.QuorumThresholdPercent,
        Iban = building.Iban
    };

    private static ApartmentSummaryDto ToDto(Apartment apartment) => new()
    {
        Id = apartment.Id,
        Number = apartment.Number,
        Floor = apartment.Floor,
        IdealParts = apartment.IdealParts,
        Budget = apartment.Budget
    };

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private async Task<Building> GetManagedBuildingOrThrowAsync(string managerId)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == managerId);
        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        return building;
    }

    private async Task<Apartment> GetOwnedApartmentOrThrowAsync(string managerId, int apartmentId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var apartment = await _context.Apartments
            .FirstOrDefaultAsync(a => a.Id == apartmentId && a.BuildingId == building.Id);

        if (apartment == null)
            throw new NotFoundException("Апартаментът не е намерен.");

        return apartment;
    }

    private async Task<JoinRequest> GetPendingJoinRequestOrThrowAsync(string managerId, int joinRequestId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var joinRequest = await _context.JoinRequests
            .Include(jr => jr.Building)
            .Include(jr => jr.Apartment)
            .FirstOrDefaultAsync(jr => jr.Id == joinRequestId
                                     && jr.BuildingId == building.Id
                                     && jr.Status == JoinRequestStatus.Pending);

        if (joinRequest == null)
            throw new NotFoundException("Заявката не е намерена.");

        return joinRequest;
    }
}
