using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Repairs;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class RepairService : IRepairService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorage _fileStorage;

    public RepairService(ApplicationDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<List<RepairListItemDto>> GetRepairsAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var repairs = await _context.Repairs
            .Where(r => r.BuildingId == building.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RepairListItemDto
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Budget = r.Budget,
                ActualCost = r.ActualCost,
                Status = (int)r.Status,
                CreatedAt = r.CreatedAt,
                CostsAllocated = _context.Fees.Any(f => f.RepairId == r.Id)
            })
            .ToListAsync();

        return repairs;
    }

    public async Task<RepairResponseDto> CreateRepairAsync(string managerId, CreateRepairDto dto)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var repair = new Repair
        {
            BuildingId = building.Id,
            Title = dto.Title,
            Description = dto.Description,
            Budget = dto.Budget,
            Status = RepairStatus.Planned
        };

        _context.Repairs.Add(repair);
        await _context.SaveChangesAsync();

        return ToDto(repair);
    }

    public async Task<RepairResponseDto> UpdateRepairAsync(string managerId, int repairId, UpdateRepairDto dto)
    {
        var repair = await GetOwnedRepairOrThrowAsync(managerId, repairId);

        if (!Enum.TryParse<RepairStatus>(dto.Status, true, out var status))
            throw new BadRequestException("Невалиден статус. Позволени стойности: Planned, InProgress, Completed.");

        repair.Title = dto.Title;
        repair.Description = dto.Description;
        repair.Budget = dto.Budget;
        repair.ActualCost = dto.ActualCost;
        repair.Status = status;

        await _context.SaveChangesAsync();

        return ToDto(repair);
    }

    public async Task DeleteRepairAsync(string managerId, int repairId)
    {
        var repair = await GetOwnedRepairOrThrowAsync(managerId, repairId);

        var costsAllocated = await _context.Fees.AnyAsync(f => f.RepairId == repairId);
        if (costsAllocated)
            throw new BadRequestException("Разходите за ремонта вече са разпределени и той не може да бъде изтрит.");

        _context.Repairs.Remove(repair);
        await _context.SaveChangesAsync();
    }

    public async Task<AllocateRepairCostsResultDto> AllocateCostsAsync(string managerId, int repairId, AllocateRepairCostsDto dto)
    {
        var repair = await GetOwnedRepairOrThrowAsync(managerId, repairId);

        if (repair.Status == RepairStatus.Planned)
            throw new BadRequestException("Ремонтът трябва да е одобрен (в процес) или завършен, преди да се разпределят разходите.");

        var alreadyAllocated = await _context.Fees.AnyAsync(f => f.RepairId == repairId);
        if (alreadyAllocated)
            throw new BadRequestException("Разходите за този ремонт вече са разпределени.");

        var totalCost = repair.ActualCost ?? repair.Budget;

        var apartments = await _context.Apartments
            .Where(a => a.BuildingId == repair.BuildingId)
            .ToListAsync();

        if (apartments.Count == 0)
            throw new BadRequestException("Сградата няма апартаменти, по които да се разпределят разходите.");

        var allocations = new Dictionary<int, decimal>();

        if (dto.ManualAllocations is { Count: > 0 })
        {
            var apartmentIds = apartments.Select(a => a.Id).ToHashSet();
            foreach (var entry in dto.ManualAllocations)
            {
                if (!apartmentIds.Contains(entry.ApartmentId))
                    throw new BadRequestException($"Апартамент {entry.ApartmentId} не принадлежи на тази сграда.");

                allocations[entry.ApartmentId] = allocations.GetValueOrDefault(entry.ApartmentId) + entry.Amount;
            }

            var manualTotal = allocations.Values.Sum();
            if (manualTotal != totalCost)
                throw new BadRequestException($"Сборът на ръчното разпределение ({manualTotal:0.00}) трябва да е равен на стойността за разпределяне ({totalCost:0.00}).");
        }
        else
        {
            decimal allocatedSoFar = 0;
            for (var i = 0; i < apartments.Count; i++)
            {
                var apartment = apartments[i];
                decimal share;
                if (i == apartments.Count - 1)
                {
                    // Last apartment absorbs the rounding remainder so the total matches exactly.
                    share = totalCost - allocatedSoFar;
                }
                else
                {
                    share = Math.Round(totalCost * apartment.IdealParts / 100m, 2);
                    allocatedSoFar += share;
                }

                allocations[apartment.Id] = share;
            }
        }

        var fee = new Fee
        {
            BuildingId = repair.BuildingId,
            RepairId = repair.Id,
            Title = repair.Title,
            Description = repair.Description,
            Amount = totalCost,
            Type = FeeType.Repair,
            Frequency = FeeFrequency.OneTime,
            DateFrom = DateTime.UtcNow,
            Priority = FeePriority.High
        };

        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        var createdObligations = allocations.Select(a => new Obligation
        {
            ApartmentId = a.Key,
            FeeId = fee.Id,
            Amount = a.Value,
            Status = ObligationStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(30)
        }).ToList();

        _context.Obligations.AddRange(createdObligations);
        await _context.SaveChangesAsync();

        return new AllocateRepairCostsResultDto
        {
            FeeId = fee.Id,
            ObligationsCreated = createdObligations.Count,
            TotalCost = totalCost
        };
    }

    public async Task<UploadedDocumentDto> UploadDocumentAsync(
        string managerId, int repairId, Stream content, string fileName, string? contentType, long length)
    {
        var repair = await GetOwnedRepairOrThrowAsync(managerId, repairId);

        var validationError = DocumentUploadValidation.Validate(length, fileName, contentType);
        if (validationError != null)
            throw new BadRequestException(validationError);

        var storagePath = await _fileStorage.SaveAsync(content, fileName);

        var document = new Document
        {
            BuildingId = repair.BuildingId,
            RepairId = repair.Id,
            FilePath = storagePath,
            FileName = fileName,
            Type = DocumentType.Invoice,
            Access = DocumentAccess.ManagerOnly
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return new UploadedDocumentDto { Id = document.Id, FileName = document.FileName, UploadedAt = document.UploadedAt };
    }

    public async Task<List<RepairDocumentDto>> GetDocumentsAsync(string managerId, int repairId)
    {
        await GetOwnedRepairOrThrowAsync(managerId, repairId);

        var documents = await _context.Documents
            .Where(d => d.RepairId == repairId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return documents.Select(d => new RepairDocumentDto
        {
            Id = d.Id,
            FileName = d.FileName,
            Type = (int)d.Type,
            UploadedAt = d.UploadedAt
        }).ToList();
    }

    private static RepairResponseDto ToDto(Repair repair) => new()
    {
        Id = repair.Id,
        Title = repair.Title,
        Description = repair.Description,
        Budget = repair.Budget,
        ActualCost = repair.ActualCost,
        Status = (int)repair.Status,
        CreatedAt = repair.CreatedAt
    };

    private async Task<Building> GetManagedBuildingOrThrowAsync(string managerId)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == managerId);
        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        return building;
    }

    private async Task<Repair> GetOwnedRepairOrThrowAsync(string managerId, int repairId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var repair = await _context.Repairs.FirstOrDefaultAsync(r => r.Id == repairId && r.BuildingId == building.Id);
        if (repair == null)
            throw new NotFoundException("Ремонтът не е намерен.");

        return repair;
    }
}
