// RepairsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Repairs;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/repairs")]
[Authorize(Policy = PolicyNames.BuildingManagement)]
public class RepairsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorage _fileStorage;

    public RepairsController(ApplicationDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    // GET: Всички ремонти на управляваната сграда
    [HttpGet]
    public async Task<IActionResult> GetRepairs()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var repairs = await _context.Repairs
            .Where(r => r.BuildingId == building.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                r.Budget,
                r.ActualCost,
                r.Status,
                r.CreatedAt,
                CostsAllocated = _context.Fees.Any(f => f.RepairId == r.Id)
            })
            .ToListAsync();

        return Ok(repairs);
    }

    // POST: Създаване на ремонт
    [HttpPost]
    public async Task<IActionResult> CreateRepair([FromBody] CreateRepairDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

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

        return Ok(RepairResponse(repair));
    }

    // PUT: Редакция на ремонт (вкл. статус и реален разход)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRepair(int id, [FromBody] UpdateRepairDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var repair = await GetOwnedRepairAsync(id);
        if (repair == null)
            return NotFound(new { message = "Ремонтът не е намерен." });

        if (!Enum.TryParse<RepairStatus>(dto.Status, true, out var status))
            return BadRequest(new { message = "Невалиден статус. Позволени стойности: Planned, InProgress, Completed." });

        repair.Title = dto.Title;
        repair.Description = dto.Description;
        repair.Budget = dto.Budget;
        repair.ActualCost = dto.ActualCost;
        repair.Status = status;

        await _context.SaveChangesAsync();

        return Ok(RepairResponse(repair));
    }

    // DELETE: Изтриване на ремонт (само ако разходите все още не са разпределени)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRepair(int id)
    {
        var repair = await GetOwnedRepairAsync(id);
        if (repair == null)
            return NotFound(new { message = "Ремонтът не е намерен." });

        var costsAllocated = await _context.Fees.AnyAsync(f => f.RepairId == id);
        if (costsAllocated)
            return BadRequest(new { message = "Разходите за ремонта вече са разпределени и той не може да бъде изтрит." });

        _context.Repairs.Remove(repair);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Ремонтът е изтрит." });
    }

    // POST: Разпределяне на разходите по апартаментите - пропорционално на идеалните части,
    // или по ръчно зададено разпределение - и създаване на съответните Obligations.
    // Позволено само веднъж на ремонт и само след като е одобрен/в процес/завършен (не Planned).
    [HttpPost("{id:int}/allocate-costs")]
    public async Task<IActionResult> AllocateCosts(int id, [FromBody] AllocateRepairCostsDto dto)
    {
        var repair = await GetOwnedRepairAsync(id);
        if (repair == null)
            return NotFound(new { message = "Ремонтът не е намерен." });

        if (repair.Status == RepairStatus.Planned)
            return BadRequest(new { message = "Ремонтът трябва да е одобрен (в процес) или завършен, преди да се разпределят разходите." });

        var alreadyAllocated = await _context.Fees.AnyAsync(f => f.RepairId == id);
        if (alreadyAllocated)
            return BadRequest(new { message = "Разходите за този ремонт вече са разпределени." });

        var totalCost = repair.ActualCost ?? repair.Budget;

        var apartments = await _context.Apartments
            .Where(a => a.BuildingId == repair.BuildingId)
            .ToListAsync();

        if (apartments.Count == 0)
            return BadRequest(new { message = "Сградата няма апартаменти, по които да се разпределят разходите." });

        var allocations = new Dictionary<int, decimal>();

        if (dto.ManualAllocations is { Count: > 0 })
        {
            var apartmentIds = apartments.Select(a => a.Id).ToHashSet();
            foreach (var entry in dto.ManualAllocations)
            {
                if (!apartmentIds.Contains(entry.ApartmentId))
                    return BadRequest(new { message = $"Апартамент {entry.ApartmentId} не принадлежи на тази сграда." });

                allocations[entry.ApartmentId] = allocations.GetValueOrDefault(entry.ApartmentId) + entry.Amount;
            }

            var manualTotal = allocations.Values.Sum();
            if (manualTotal != totalCost)
                return BadRequest(new { message = $"Сборът на ръчното разпределение ({manualTotal:0.00}) трябва да е равен на стойността за разпределяне ({totalCost:0.00})." });
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

        return Ok(new { feeId = fee.Id, obligationsCreated = createdObligations.Count, totalCost });
    }

    // POST: Прикачване на фактура/документ към ремонт
    [HttpPost("{id:int}/documents")]
    [RequestSizeLimit(DocumentUploadValidation.MaxBytes)]
    public async Task<IActionResult> UploadDocument(int id, IFormFile file)
    {
        var repair = await GetOwnedRepairAsync(id);
        if (repair == null)
            return NotFound(new { message = "Ремонтът не е намерен." });

        var validationError = DocumentUploadValidation.Validate(file);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        await using var stream = file.OpenReadStream();
        var storagePath = await _fileStorage.SaveAsync(stream, file.FileName);

        var document = new Document
        {
            BuildingId = repair.BuildingId,
            RepairId = repair.Id,
            FilePath = storagePath,
            FileName = file.FileName,
            Type = DocumentType.Invoice,
            Access = DocumentAccess.ManagerOnly
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return Ok(new { document.Id, document.FileName, document.UploadedAt });
    }

    // GET: Прикачените документи на ремонт
    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> GetDocuments(int id)
    {
        var repair = await GetOwnedRepairAsync(id);
        if (repair == null)
            return NotFound(new { message = "Ремонтът не е намерен." });

        var documents = await _context.Documents
            .Where(d => d.RepairId == id)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new { d.Id, d.FileName, d.Type, d.UploadedAt })
            .ToListAsync();

        return Ok(documents);
    }

    private static object RepairResponse(Repair repair) => new
    {
        repair.Id,
        repair.Title,
        repair.Description,
        repair.Budget,
        repair.ActualCost,
        repair.Status,
        repair.CreatedAt
    };

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private Task<Building?> GetManagedBuildingAsync() =>
        _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);

    private async Task<Repair?> GetOwnedRepairAsync(int id)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return null;

        return await _context.Repairs.FirstOrDefaultAsync(r => r.Id == id && r.BuildingId == building.Id);
    }
}
