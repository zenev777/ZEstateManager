using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Fees;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class FeeService : IFeeService
{
    private readonly ApplicationDbContext _context;
    private readonly IObligationGenerationService _obligationGenerationService;
    private readonly IObligationStatusService _obligationStatusService;

    public FeeService(
        ApplicationDbContext context,
        IObligationGenerationService obligationGenerationService,
        IObligationStatusService obligationStatusService)
    {
        _context = context;
        _obligationGenerationService = obligationGenerationService;
        _obligationStatusService = obligationStatusService;
    }

    public async Task<List<FeeResponseDto>> GetFeesAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var fees = await _context.Fees
            .Where(f => f.BuildingId == building.Id)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return fees.Select(ToDto).ToList();
    }

    public async Task<FeeResponseDto> CreateFeeAsync(string managerId, CreateFeeDto dto)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        if (!Enum.TryParse<FeeType>(dto.Type, true, out var type))
            throw new BadRequestException("Невалиден тип такса. Позволени стойности: Fixed, PerIdealPart.");

        if (!Enum.TryParse<FeeFrequency>(dto.Frequency, true, out var frequency))
            throw new BadRequestException("Невалидна периодичност. Позволени стойности: OneTime, Monthly.");

        if (!Enum.TryParse<FeePriority>(dto.Priority, true, out var priority))
            throw new BadRequestException("Невалиден приоритет.");

        var fee = new Fee
        {
            BuildingId = building.Id,
            Title = dto.Title,
            Description = dto.Description,
            Amount = dto.Amount,
            Type = type,
            Frequency = frequency,
            DateFrom = dto.DateFrom,
            DateTo = dto.DateTo,
            Priority = priority
        };

        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        return ToDto(fee);
    }

    public async Task<FeeResponseDto> UpdateFeeAsync(string managerId, int feeId, UpdateFeeDto dto)
    {
        var fee = await GetOwnedFeeOrThrowAsync(managerId, feeId);

        if (!Enum.TryParse<FeeType>(dto.Type, true, out var type))
            throw new BadRequestException("Невалиден тип такса. Позволени стойности: Fixed, PerIdealPart.");

        if (!Enum.TryParse<FeeFrequency>(dto.Frequency, true, out var frequency))
            throw new BadRequestException("Невалидна периодичност. Позволени стойности: OneTime, Monthly.");

        if (!Enum.TryParse<FeePriority>(dto.Priority, true, out var priority))
            throw new BadRequestException("Невалиден приоритет.");

        fee.Title = dto.Title;
        fee.Description = dto.Description;
        fee.Amount = dto.Amount;
        fee.Type = type;
        fee.Frequency = frequency;
        fee.DateFrom = dto.DateFrom;
        fee.DateTo = dto.DateTo;
        fee.Priority = priority;

        await _context.SaveChangesAsync();

        return ToDto(fee);
    }

    public async Task DeleteFeeAsync(string managerId, int feeId)
    {
        var fee = await GetOwnedFeeOrThrowAsync(managerId, feeId);

        var hasObligations = await _context.Obligations.AnyAsync(o => o.FeeId == feeId);
        if (hasObligations)
            throw new BadRequestException("Таксата има генерирани задължения и не може да бъде изтрита.");

        _context.Fees.Remove(fee);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ObligationSummaryDto>> GetObligationsAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var obligations = await _context.Obligations
            .Where(o => o.Apartment.BuildingId == building.Id)
            .Include(o => o.Apartment)
            .Include(o => o.Fee)
            .OrderByDescending(o => o.DateCreated)
            .ToListAsync();

        return obligations.Select(o => new ObligationSummaryDto
        {
            Id = o.Id,
            ApartmentNumber = o.Apartment.Number,
            FeeTitle = o.Fee.Title,
            Amount = o.Amount,
            Status = (int)o.Status,
            Period = o.Period,
            DueDate = o.DueDate,
            DateCreated = o.DateCreated
        }).ToList();
    }

    public async Task<ObligationsSummaryDto> GetObligationsSummaryAsync(string managerId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var counts = await _context.Obligations
            .Where(o => o.Apartment.BuildingId == building.Id)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(o => o.Amount) })
            .ToListAsync();

        ObligationsStatusBucketDto Summarize(ObligationStatus status)
        {
            var match = counts.FirstOrDefault(c => c.Status == status);
            return new ObligationsStatusBucketDto { Count = match?.Count ?? 0, Total = match?.Total ?? 0m };
        }

        return new ObligationsSummaryDto
        {
            Pending = Summarize(ObligationStatus.Pending),
            PartiallyPaid = Summarize(ObligationStatus.PartiallyPaid),
            Paid = Summarize(ObligationStatus.Paid),
            Overdue = Summarize(ObligationStatus.Overdue)
        };
    }

    public async Task<List<ObligationSummaryDto>> GetMyObligationsAsync(string userId)
    {
        var apartmentId = await _context.ApartmentUsers
            .Where(au => au.UserId == userId && au.IsActive)
            .Select(au => (int?)au.ApartmentId)
            .FirstOrDefaultAsync();

        if (apartmentId == null)
            return new List<ObligationSummaryDto>();

        var obligations = await _context.Obligations
            .Where(o => o.ApartmentId == apartmentId.Value)
            .Include(o => o.Apartment)
            .Include(o => o.Fee)
            .OrderByDescending(o => o.DateCreated)
            .ToListAsync();

        return obligations.Select(o => new ObligationSummaryDto
        {
            Id = o.Id,
            ApartmentNumber = o.Apartment.Number,
            FeeTitle = o.Fee.Title,
            Amount = o.Amount,
            Status = (int)o.Status,
            Period = o.Period,
            DueDate = o.DueDate,
            DateCreated = o.DateCreated
        }).ToList();
    }

    public Task<ObligationGenerationResult> GenerateObligationsAsync() =>
        _obligationGenerationService.GenerateForCurrentPeriodAsync();

    public Task<ObligationGenerationPreview> PreviewObligationsAsync() =>
        _obligationGenerationService.PreviewForCurrentPeriodAsync();

    public Task<int> MarkOverdueAsync() =>
        _obligationStatusService.MarkOverdueAsync();

    private static FeeResponseDto ToDto(Fee fee) => new()
    {
        Id = fee.Id,
        Title = fee.Title,
        Description = fee.Description,
        Amount = fee.Amount,
        Type = (int)fee.Type,
        Frequency = (int)fee.Frequency,
        DateFrom = fee.DateFrom,
        DateTo = fee.DateTo,
        Priority = (int)fee.Priority,
        CreatedAt = fee.CreatedAt
    };

    private async Task<Building> GetManagedBuildingOrThrowAsync(string managerId)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == managerId);
        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        return building;
    }

    private async Task<Fee> GetOwnedFeeOrThrowAsync(string managerId, int feeId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var fee = await _context.Fees.FirstOrDefaultAsync(f => f.Id == feeId && f.BuildingId == building.Id);
        if (fee == null)
            throw new NotFoundException("Таксата не е намерена.");

        return fee;
    }
}
