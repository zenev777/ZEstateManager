using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Payments;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;

    public PaymentService(ApplicationDbContext context)
    {
        _context = context;
    }

    // Разпределя се по най-старите неплатени задължения на апартамента; евентуален
    // остатък (надплащане) отива като кредит към бюджета на апартамента.
    public async Task<RegisterPaymentResultDto> RegisterPaymentAsync(string userId, RegisterPaymentDto dto)
    {
        if (!Enum.TryParse<PaymentMethod>(dto.Method, true, out var method))
            throw new BadRequestException("Невалиден метод на плащане. Позволени стойности: Manual, Stripe.");

        var apartment = await GetOwnedApartmentOrThrowAsync(userId, dto.ApartmentId);

        var outstandingObligations = await _context.Obligations
            .Where(o => o.ApartmentId == apartment.Id
                     && (o.Status == ObligationStatus.Pending || o.Status == ObligationStatus.PartiallyPaid))
            .Include(o => o.Payments)
            .Include(o => o.Fee)
            .OrderBy(o => o.DueDate ?? DateTime.MaxValue)
            .ThenBy(o => o.DateCreated)
            .ToListAsync();

        var remaining = dto.Amount;
        var allocations = new List<PaymentAllocationDto>();

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

            allocations.Add(new PaymentAllocationDto
            {
                Id = obligation.Id,
                FeeTitle = obligation.Fee.Title,
                AmountApplied = allocation,
                Status = (int)obligation.Status
            });
        }

        decimal creditApplied = 0;
        if (remaining > 0)
        {
            apartment.Budget += remaining;
            creditApplied = remaining;
        }

        await _context.SaveChangesAsync();

        return new RegisterPaymentResultDto
        {
            TotalAmount = dto.Amount,
            Allocations = allocations,
            CreditApplied = creditApplied
        };
    }

    public async Task<List<PaymentSummaryDto>> GetPaymentsAsync(string userId, int? apartmentId, DateTime? from, DateTime? to)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);

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

        var payments = await query.OrderByDescending(p => p.PaidAt).ToListAsync();

        return payments.Select(p => new PaymentSummaryDto
        {
            Id = p.Id,
            ApartmentNumber = p.Obligation.Apartment.Number,
            FeeTitle = p.Obligation.Fee.Title,
            Amount = p.Amount,
            PaidAt = p.PaidAt,
            Method = (int)p.Method,
            Note = p.Note
        }).ToList();
    }

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

    private async Task<Apartment> GetOwnedApartmentOrThrowAsync(string userId, int apartmentId)
    {
        var building = await GetManagedBuildingOrThrowAsync(userId);

        var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.Id == apartmentId && a.BuildingId == building.Id);
        if (apartment == null)
            throw new NotFoundException("Апартаментът не е намерен.");

        return apartment;
    }
}
