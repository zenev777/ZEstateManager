using ZEstate.Core.DTOs.Payments;
using ZEstate.Core.Exceptions;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class PaymentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PaymentService _service;
    private const string ManagerId = "mgr1";

    public PaymentServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new PaymentService(_context);
    }

    public void Dispose() => _context.Dispose();

    private (Building Building, Apartment Apartment) AddManagedBuildingWithApartment()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        var apartment = new Apartment { Building = building, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.SaveChanges();
        return (building, apartment);
    }

    [Fact]
    public async Task RegisterPaymentAsync_InvalidMethod_ThrowsBadRequest()
    {
        var (_, apartment) = AddManagedBuildingWithApartment();
        var dto = new RegisterPaymentDto { ApartmentId = apartment.Id, Amount = 10, PaidAt = DateTime.UtcNow, Method = "NotReal" };

        await Assert.ThrowsAsync<BadRequestException>(() => _service.RegisterPaymentAsync(ManagerId, dto));
    }

    [Fact]
    public async Task RegisterPaymentAsync_UnknownApartment_ThrowsNotFound()
    {
        AddManagedBuildingWithApartment();
        var dto = new RegisterPaymentDto { ApartmentId = 999, Amount = 10, PaidAt = DateTime.UtcNow, Method = "Manual" };

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RegisterPaymentAsync(ManagerId, dto));
    }

    [Fact]
    public async Task RegisterPaymentAsync_AppliesToOldestObligationFirst()
    {
        var (building, apartment) = AddManagedBuildingWithApartment();
        var fee = new Fee { BuildingId = building.Id, Title = "F", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        var older = new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 30, Status = ObligationStatus.Pending, DueDate = DateTime.UtcNow.AddDays(-5) };
        var newer = new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 20, Status = ObligationStatus.Pending, DueDate = DateTime.UtcNow.AddDays(5) };
        _context.Obligations.AddRange(older, newer);
        await _context.SaveChangesAsync();

        var dto = new RegisterPaymentDto { ApartmentId = apartment.Id, Amount = 30, PaidAt = DateTime.UtcNow, Method = "Manual" };
        var result = await _service.RegisterPaymentAsync(ManagerId, dto);

        Assert.Single(result.Allocations);
        Assert.Equal(30, result.Allocations[0].AmountApplied);
        Assert.Equal((int)ObligationStatus.Paid, result.Allocations[0].Status);
        Assert.Equal(ObligationStatus.Pending, _context.Obligations.Single(o => o.Id == newer.Id).Status);
        Assert.Equal(0, result.CreditApplied);
    }

    [Fact]
    public async Task RegisterPaymentAsync_PartialPayment_MarksPartiallyPaid()
    {
        var (building, apartment) = AddManagedBuildingWithApartment();
        var fee = new Fee { BuildingId = building.Id, Title = "F", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();
        _context.Obligations.Add(new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 50, Status = ObligationStatus.Pending });
        await _context.SaveChangesAsync();

        var dto = new RegisterPaymentDto { ApartmentId = apartment.Id, Amount = 20, PaidAt = DateTime.UtcNow, Method = "Manual" };
        var result = await _service.RegisterPaymentAsync(ManagerId, dto);

        Assert.Equal((int)ObligationStatus.PartiallyPaid, result.Allocations[0].Status);
        Assert.Equal(0, result.CreditApplied);
    }

    [Fact]
    public async Task RegisterPaymentAsync_Overpayment_CreditsApartmentBudget()
    {
        var (building, apartment) = AddManagedBuildingWithApartment();
        var fee = new Fee { BuildingId = building.Id, Title = "F", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();
        _context.Obligations.Add(new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 10, Status = ObligationStatus.Pending });
        await _context.SaveChangesAsync();

        var dto = new RegisterPaymentDto { ApartmentId = apartment.Id, Amount = 15, PaidAt = DateTime.UtcNow, Method = "Manual" };
        var result = await _service.RegisterPaymentAsync(ManagerId, dto);

        Assert.Equal(5, result.CreditApplied);
        Assert.Equal(5, _context.Apartments.Single().Budget);
    }

    [Fact]
    public async Task RegisterPaymentAsync_NoOutstandingObligations_EntireAmountBecomesCredit()
    {
        var (_, apartment) = AddManagedBuildingWithApartment();

        var dto = new RegisterPaymentDto { ApartmentId = apartment.Id, Amount = 25, PaidAt = DateTime.UtcNow, Method = "Manual" };
        var result = await _service.RegisterPaymentAsync(ManagerId, dto);

        Assert.Empty(result.Allocations);
        Assert.Equal(25, result.CreditApplied);
    }

    [Fact]
    public async Task GetPaymentsAsync_FiltersByApartmentAndDateRange()
    {
        var (building, apartment) = AddManagedBuildingWithApartment();
        var otherApartment = new Apartment { BuildingId = building.Id, Number = "2", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(otherApartment);
        var fee = new Fee { BuildingId = building.Id, Title = "F", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        var obligationA = new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 10, Status = ObligationStatus.Paid };
        var obligationB = new Obligation { ApartmentId = otherApartment.Id, FeeId = fee.Id, Amount = 10, Status = ObligationStatus.Paid };
        _context.Obligations.AddRange(obligationA, obligationB);
        await _context.SaveChangesAsync();

        _context.Payments.AddRange(
            new Payment { ObligationId = obligationA.Id, Amount = 10, PaidAt = DateTime.UtcNow.AddDays(-1), Method = PaymentMethod.Manual },
            new Payment { ObligationId = obligationB.Id, Amount = 10, PaidAt = DateTime.UtcNow.AddDays(-1), Method = PaymentMethod.Manual });
        await _context.SaveChangesAsync();

        var result = await _service.GetPaymentsAsync(ManagerId, apartment.Id, null, null);

        Assert.Single(result);
        Assert.Equal("1", result[0].ApartmentNumber);
    }
}
