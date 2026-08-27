// PaymentsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Payments;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/payments")]
[Authorize(Policy = PolicyNames.PaymentsManagement)]
public class PaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PaymentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // POST: Ръчно регистриране на плащане - разпределя се по най-старите неплатени
    // задължения на апартамента; евентуален остатък (надплащане) отива като кредит
    // към бюджета на апартамента.
    [HttpPost]
    public async Task<IActionResult> RegisterPayment([FromBody] RegisterPaymentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!Enum.TryParse<PaymentMethod>(dto.Method, true, out var method))
            return BadRequest(new { message = "Невалиден метод на плащане. Позволени стойности: Manual, Stripe." });

        var apartment = await GetOwnedApartmentAsync(dto.ApartmentId);
        if (apartment == null)
            return NotFound(new { message = "Апартаментът не е намерен." });

        var outstandingObligations = await _context.Obligations
            .Where(o => o.ApartmentId == apartment.Id
                     && (o.Status == ObligationStatus.Pending || o.Status == ObligationStatus.PartiallyPaid))
            .Include(o => o.Payments)
            .Include(o => o.Fee)
            .OrderBy(o => o.DueDate ?? DateTime.MaxValue)
            .ThenBy(o => o.DateCreated)
            .ToListAsync();

        var remaining = dto.Amount;
        var allocations = new List<object>();

        foreach (var obligation in outstandingObligations)
        {
            if (remaining <= 0)
                break;

            var alreadyPaid = obligation.Payments.Sum(p => p.Amount);
            var outstandingBalance = obligation.Amount - alreadyPaid;
            if (outstandingBalance <= 0)
                continue;

            var allocation = Math.Min(remaining, outstandingBalance);

            _context.Payments.Add(new Payment
            {
                ObligationId = obligation.Id,
                Amount = allocation,
                PaidAt = dto.PaidAt,
                Method = method,
                Note = dto.Note
            });

            remaining -= allocation;
            obligation.Status = (alreadyPaid + allocation) >= obligation.Amount
                ? ObligationStatus.Paid
                : ObligationStatus.PartiallyPaid;

            allocations.Add(new
            {
                obligation.Id,
                FeeTitle = obligation.Fee.Title,
                AmountApplied = allocation,
                obligation.Status
            });
        }

        decimal creditApplied = 0;
        if (remaining > 0)
        {
            apartment.Budget += remaining;
            creditApplied = remaining;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            totalAmount = dto.Amount,
            allocations,
            creditApplied
        });
    }

    // GET: История на плащанията по апартамент, опционално филтрируема по период
    [HttpGet]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int? apartmentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var query = _context.Payments
            .Where(p => p.Obligation.Apartment.BuildingId == building.Id)
            .Include(p => p.Obligation).ThenInclude(o => o.Apartment)
            .Include(p => p.Obligation).ThenInclude(o => o.Fee)
            .AsQueryable();

        if (apartmentId.HasValue)
            query = query.Where(p => p.Obligation.ApartmentId == apartmentId.Value);

        if (from.HasValue)
            query = query.Where(p => p.PaidAt >= from.Value);

        if (to.HasValue)
            query = query.Where(p => p.PaidAt <= to.Value);

        var payments = await query
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new
            {
                p.Id,
                ApartmentNumber = p.Obligation.Apartment.Number,
                FeeTitle = p.Obligation.Fee.Title,
                p.Amount,
                p.PaidAt,
                p.Method,
                p.Note
            })
            .ToListAsync();

        return Ok(payments);
    }

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

    private async Task<Apartment?> GetOwnedApartmentAsync(int id)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return null;

        return await _context.Apartments.FirstOrDefaultAsync(a => a.Id == id && a.BuildingId == building.Id);
    }
}
