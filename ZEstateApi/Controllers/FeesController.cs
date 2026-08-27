// FeesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Fees;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/fees")]
[Authorize(Policy = PolicyNames.BuildingManagement)]
public class FeesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IObligationGenerationService _obligationGenerationService;

    public FeesController(ApplicationDbContext context, IObligationGenerationService obligationGenerationService)
    {
        _context = context;
        _obligationGenerationService = obligationGenerationService;
    }

    // GET: Всички такси на управляваната сграда
    [HttpGet]
    public async Task<IActionResult> GetFees()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var fees = await _context.Fees
            .Where(f => f.BuildingId == building.Id)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => FeeResponse(f))
            .ToListAsync();

        return Ok(fees);
    }

    // POST: Създаване на такса
    [HttpPost]
    public async Task<IActionResult> CreateFee([FromBody] CreateFeeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        if (!Enum.TryParse<FeeType>(dto.Type, true, out var type))
            return BadRequest(new { message = "Невалиден тип такса. Позволени стойности: Fixed, PerIdealPart." });

        if (!Enum.TryParse<FeeFrequency>(dto.Frequency, true, out var frequency))
            return BadRequest(new { message = "Невалидна периодичност. Позволени стойности: OneTime, Monthly." });

        if (!Enum.TryParse<FeePriority>(dto.Priority, true, out var priority))
            return BadRequest(new { message = "Невалиден приоритет." });

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

        return Ok(FeeResponse(fee));
    }

    // PUT: Редакция на такса
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFee(int id, [FromBody] UpdateFeeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var fee = await GetOwnedFeeAsync(id);
        if (fee == null)
            return NotFound(new { message = "Таксата не е намерена." });

        if (!Enum.TryParse<FeeType>(dto.Type, true, out var type))
            return BadRequest(new { message = "Невалиден тип такса. Позволени стойности: Fixed, PerIdealPart." });

        if (!Enum.TryParse<FeeFrequency>(dto.Frequency, true, out var frequency))
            return BadRequest(new { message = "Невалидна периодичност. Позволени стойности: OneTime, Monthly." });

        if (!Enum.TryParse<FeePriority>(dto.Priority, true, out var priority))
            return BadRequest(new { message = "Невалиден приоритет." });

        fee.Title = dto.Title;
        fee.Description = dto.Description;
        fee.Amount = dto.Amount;
        fee.Type = type;
        fee.Frequency = frequency;
        fee.DateFrom = dto.DateFrom;
        fee.DateTo = dto.DateTo;
        fee.Priority = priority;

        await _context.SaveChangesAsync();

        return Ok(FeeResponse(fee));
    }

    // DELETE: Изтриване на такса (само ако все още няма генерирани задължения по нея)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFee(int id)
    {
        var fee = await GetOwnedFeeAsync(id);
        if (fee == null)
            return NotFound(new { message = "Таксата не е намерена." });

        var hasObligations = await _context.Obligations.AnyAsync(o => o.FeeId == id);
        if (hasObligations)
            return BadRequest(new { message = "Таксата има генерирани задължения и не може да бъде изтрита." });

        _context.Fees.Remove(fee);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Таксата е изтрита." });
    }

    // POST: Ръчно стартиране на генерирането на задължения за текущия период (демо/тест удобство -
    // същата логика, която фоновата задача пуска автоматично всеки ден)
    [HttpPost("generate-obligations")]
    public async Task<IActionResult> GenerateObligations()
    {
        var result = await _obligationGenerationService.GenerateForCurrentPeriodAsync();
        return Ok(new { result.Created, result.SkippedExisting });
    }

    // GET: Генерираните задължения на управляваната сграда
    [HttpGet("obligations")]
    public async Task<IActionResult> GetObligations()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var obligations = await _context.Obligations
            .Where(o => o.Apartment.BuildingId == building.Id)
            .Include(o => o.Apartment)
            .Include(o => o.Fee)
            .OrderByDescending(o => o.DateCreated)
            .Select(o => new
            {
                o.Id,
                ApartmentNumber = o.Apartment.Number,
                FeeTitle = o.Fee.Title,
                o.Amount,
                o.Status,
                o.Period,
                o.DueDate,
                o.DateCreated
            })
            .ToListAsync();

        return Ok(obligations);
    }

    private static object FeeResponse(Fee fee) => new
    {
        fee.Id,
        fee.Title,
        fee.Description,
        fee.Amount,
        fee.Type,
        fee.Frequency,
        fee.DateFrom,
        fee.DateTo,
        fee.Priority,
        fee.CreatedAt
    };

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private Task<Building?> GetManagedBuildingAsync() =>
        _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);

    private async Task<Fee?> GetOwnedFeeAsync(int id)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return null;

        return await _context.Fees.FirstOrDefaultAsync(f => f.Id == id && f.BuildingId == building.Id);
    }
}
