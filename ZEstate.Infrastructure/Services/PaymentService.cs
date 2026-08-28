using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZEstate.Core.DTOs.Payments;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IConfiguration _configuration;

    public PaymentService(ApplicationDbContext context, IPaymentGateway paymentGateway, IConfiguration configuration)
    {
        _context = context;
        _paymentGateway = paymentGateway;
        _configuration = configuration;
    }

    // Разпределя се по най-старите неплатени задължения на апартамента; евентуален
    // остатък (надплащане) отива като кредит към бюджета на апартамента.
    public async Task<RegisterPaymentResultDto> RegisterPaymentAsync(string userId, RegisterPaymentDto dto)
    {
        if (!Enum.TryParse<PaymentMethod>(dto.Method, true, out var method))
            throw new BadRequestException("Невалиден метод на плащане. Позволени стойности: Manual, Stripe.");

        // Stripe money always lands in the bank account; a manually-recorded payment
        // defaults to Cash unless the caller says it was actually a bank transfer.
        CashAccountType account;
        if (method == PaymentMethod.Stripe)
        {
            account = CashAccountType.Bank;
        }
        else if (string.IsNullOrWhiteSpace(dto.Account))
        {
            account = CashAccountType.Cash;
        }
        else if (!Enum.TryParse(dto.Account, true, out account))
        {
            throw new BadRequestException("Невалидна каса. Позволени стойности: Cash, Bank.");
        }

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

        _context.CashLedgerEntries.Add(new CashLedgerEntry
        {
            BuildingId = apartment.BuildingId,
            Account = account,
            Amount = dto.Amount,
            Description = $"Плащане, апартамент {apartment.Number}",
            CreatedByUserId = userId
        });

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

    // Резидентът плаща собствено задължение с карта през Stripe Checkout. Изисква
    // сградата вече да е задала IBAN (за бъдещо реално разпределяне на парите).
    public async Task<CheckoutSessionUrlDto> CreateCheckoutSessionAsync(string userId, int obligationId)
    {
        var obligation = await _context.Obligations
            .Include(o => o.Apartment).ThenInclude(a => a.Building)
            .Include(o => o.Fee)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == obligationId);

        if (obligation == null)
            throw new NotFoundException("Задължението не е намерено.");

        var myApartmentId = await _context.ApartmentUsers
            .Where(au => au.UserId == userId && au.IsActive)
            .Select(au => (int?)au.ApartmentId)
            .FirstOrDefaultAsync();

        if (myApartmentId == null || obligation.ApartmentId != myApartmentId.Value)
            throw new ForbiddenException();

        if (obligation.Status == ObligationStatus.Paid)
            throw new BadRequestException("Задължението вече е платено.");

        var building = obligation.Apartment.Building;
        if (string.IsNullOrWhiteSpace(building.Iban))
            throw new BadRequestException("Сградата няма зададен IBAN за получаване на плащания. Свържи се с домоуправителя.");

        var remaining = obligation.Amount - obligation.Payments.Sum(p => p.Amount);
        if (remaining <= 0)
            throw new BadRequestException("Няма дължима сума по това задължение.");

        var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";

        var session = await _paymentGateway.CreateCheckoutSessionAsync(
            remaining,
            "eur",
            obligation.Fee.Title,
            $"{frontendBaseUrl}/dashboard/fees?checkout=success",
            $"{frontendBaseUrl}/dashboard/fees?checkout=cancel",
            new Dictionary<string, string> { ["obligationId"] = obligation.Id.ToString() });

        return new CheckoutSessionUrlDto { CheckoutUrl = session.CheckoutUrl };
    }

    public async Task HandleStripeWebhookAsync(string payload, string signatureHeader)
    {
        var completed = _paymentGateway.ParseCheckoutCompletedWebhook(payload, signatureHeader);
        if (completed == null)
            return;

        if (!completed.Metadata.TryGetValue("obligationId", out var obligationIdRaw) || !int.TryParse(obligationIdRaw, out var obligationId))
            return;

        var obligation = await _context.Obligations
            .Include(o => o.Payments)
            .Include(o => o.Apartment)
            .FirstOrDefaultAsync(o => o.Id == obligationId);

        // Already paid (or gone) - nothing to do. Stripe may retry webhook delivery,
        // so this also makes the handler idempotent against duplicate events.
        if (obligation == null || obligation.Status == ObligationStatus.Paid)
            return;

        var alreadyPaid = obligation.Payments.Sum(p => p.Amount);

        _context.Payments.Add(new Payment
        {
            ObligationId = obligation.Id,
            Amount = completed.AmountTotal,
            PaidAt = DateTime.UtcNow,
            Method = PaymentMethod.Stripe,
            Note = $"Stripe checkout {completed.SessionId}"
        });

        obligation.Status = (alreadyPaid + completed.AmountTotal) >= obligation.Amount
            ? ObligationStatus.Paid
            : ObligationStatus.PartiallyPaid;

        // Card payments always land in the bank account, never physical cash.
        _context.CashLedgerEntries.Add(new CashLedgerEntry
        {
            BuildingId = obligation.Apartment.BuildingId,
            Account = CashAccountType.Bank,
            Amount = completed.AmountTotal,
            Description = $"Stripe плащане, апартамент {obligation.Apartment.Number}"
        });

        await _context.SaveChangesAsync();
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
