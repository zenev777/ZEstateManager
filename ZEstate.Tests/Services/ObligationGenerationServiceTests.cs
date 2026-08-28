using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class ObligationGenerationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<INotificationService> _notificationService;
    private readonly ObligationGenerationService _service;

    public ObligationGenerationServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _notificationService = new Mock<INotificationService>();
        _service = new ObligationGenerationService(
            _context, NullLogger<ObligationGenerationService>.Instance, _notificationService.Object);
    }

    public void Dispose() => _context.Dispose();

    private Apartment SeedApartmentWithActiveResident(Building building, string userId)
    {
        var apartment = new Apartment { Building = building, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.SaveChanges();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = userId, IsActive = true });
        _context.SaveChanges();
        return apartment;
    }

    [Fact]
    public async Task GenerateForCurrentPeriodAsync_NewObligation_NotifiesActiveResident()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1" };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        var apartment = SeedApartmentWithActiveResident(building, "res1");

        _context.Fees.Add(new Fee
        {
            Building = building,
            Title = "Такса поддръжка",
            Amount = 25,
            Type = FeeType.Fixed,
            Frequency = FeeFrequency.Monthly,
            DateFrom = DateTime.UtcNow.AddMonths(-1)
        });
        await _context.SaveChangesAsync();

        var result = await _service.GenerateForCurrentPeriodAsync();

        Assert.Equal(1, result.Created);
        Assert.Single(_context.Obligations);
        _notificationService.Verify(n => n.NotifyAsync(
            "res1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task GenerateForCurrentPeriodAsync_InactiveResident_DoesNotNotify()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1" };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        var apartment = new Apartment { Building = building, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.SaveChanges();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "res1", IsActive = false });
        await _context.SaveChangesAsync();

        _context.Fees.Add(new Fee
        {
            Building = building,
            Title = "Такса поддръжка",
            Amount = 25,
            Type = FeeType.Fixed,
            Frequency = FeeFrequency.Monthly,
            DateFrom = DateTime.UtcNow.AddMonths(-1)
        });
        await _context.SaveChangesAsync();

        await _service.GenerateForCurrentPeriodAsync();

        _notificationService.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task GenerateForCurrentPeriodAsync_AlreadyExists_DoesNotNotifyAgain()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1" };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        var apartment = SeedApartmentWithActiveResident(building, "res1");

        var fee = new Fee
        {
            Building = building,
            Title = "Такса поддръжка",
            Amount = 25,
            Type = FeeType.Fixed,
            Frequency = FeeFrequency.Monthly,
            DateFrom = DateTime.UtcNow.AddMonths(-1)
        };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        var currentPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _context.Obligations.Add(new Obligation
        {
            ApartmentId = apartment.Id,
            FeeId = fee.Id,
            Amount = 25,
            Status = ObligationStatus.Pending,
            Period = currentPeriod
        });
        await _context.SaveChangesAsync();

        var result = await _service.GenerateForCurrentPeriodAsync();

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.SkippedExisting);
        _notificationService.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
    }
}
