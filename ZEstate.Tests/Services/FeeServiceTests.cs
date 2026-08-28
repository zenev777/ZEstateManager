using Moq;
using ZEstate.Core.DTOs.Fees;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class FeeServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IObligationGenerationService> _generation = new();
    private readonly Mock<IObligationStatusService> _status = new();
    private readonly FeeService _service;
    private const string ManagerId = "mgr1";

    public FeeServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new FeeService(_context, _generation.Object, _status.Object);
    }

    public void Dispose() => _context.Dispose();

    private Building AddManagedBuilding()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        return building;
    }

    private static CreateFeeDto ValidCreateDto() => new()
    {
        Title = "Fee",
        Amount = 10,
        Type = "Fixed",
        Frequency = "Monthly",
        DateFrom = DateTime.UtcNow,
        Priority = "Normal"
    };

    [Fact]
    public async Task GetFeesAsync_NoManagedBuilding_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetFeesAsync(ManagerId));
    }

    [Fact]
    public async Task CreateFeeAsync_InvalidType_ThrowsBadRequest()
    {
        AddManagedBuilding();
        var dto = ValidCreateDto();
        dto.Type = "NotReal";

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateFeeAsync(ManagerId, dto));
    }

    [Fact]
    public async Task CreateFeeAsync_InvalidFrequency_ThrowsBadRequest()
    {
        AddManagedBuilding();
        var dto = ValidCreateDto();
        dto.Frequency = "NotReal";

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateFeeAsync(ManagerId, dto));
    }

    [Fact]
    public async Task CreateFeeAsync_InvalidPriority_ThrowsBadRequest()
    {
        AddManagedBuilding();
        var dto = ValidCreateDto();
        dto.Priority = "NotReal";

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateFeeAsync(ManagerId, dto));
    }

    [Fact]
    public async Task CreateFeeAsync_Valid_PersistsAndReturnsNumericEnums()
    {
        AddManagedBuilding();
        var dto = ValidCreateDto();

        var result = await _service.CreateFeeAsync(ManagerId, dto);

        Assert.Equal((int)FeeType.Fixed, result.Type);
        Assert.Equal((int)FeeFrequency.Monthly, result.Frequency);
        Assert.Equal((int)FeePriority.Normal, result.Priority);
        Assert.Single(_context.Fees);
    }

    [Fact]
    public async Task UpdateFeeAsync_NotFound_ThrowsNotFound()
    {
        AddManagedBuilding();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateFeeAsync(ManagerId, 999, new UpdateFeeDto { Title = "X", Amount = 1, Type = "Fixed", Frequency = "Monthly", DateFrom = DateTime.UtcNow }));
    }

    [Fact]
    public async Task DeleteFeeAsync_WithObligations_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var fee = new Fee { BuildingId = building.Id, Title = "F", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();
        _context.Obligations.Add(new Obligation { ApartmentId = 1, FeeId = fee.Id, Amount = 10, Status = ObligationStatus.Pending });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => _service.DeleteFeeAsync(ManagerId, fee.Id));
    }

    [Fact]
    public async Task DeleteFeeAsync_NoObligations_Deletes()
    {
        var building = AddManagedBuilding();
        var fee = new Fee { BuildingId = building.Id, Title = "F", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        await _service.DeleteFeeAsync(ManagerId, fee.Id);

        Assert.Empty(_context.Fees);
    }

    [Fact]
    public async Task GetObligationsSummaryAsync_GroupsByStatusWithZeroDefaults()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        var fee = new Fee { BuildingId = building.Id, Title = "F", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        _context.Obligations.AddRange(
            new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 10, Status = ObligationStatus.Pending },
            new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 20, Status = ObligationStatus.Pending },
            new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 15, Status = ObligationStatus.Paid });
        await _context.SaveChangesAsync();

        var summary = await _service.GetObligationsSummaryAsync(ManagerId);

        Assert.Equal(2, summary.Pending.Count);
        Assert.Equal(30, summary.Pending.Total);
        Assert.Equal(1, summary.Paid.Count);
        Assert.Equal(15, summary.Paid.Total);
        Assert.Equal(0, summary.Overdue.Count);
        Assert.Equal(0, summary.Overdue.Total);
    }

    [Fact]
    public async Task GenerateObligationsAsync_DelegatesToObligationGenerationService()
    {
        _generation.Setup(g => g.GenerateForCurrentPeriodAsync()).ReturnsAsync(new ObligationGenerationResult(3, 1));

        var result = await _service.GenerateObligationsAsync();

        Assert.Equal(3, result.Created);
        Assert.Equal(1, result.SkippedExisting);
    }

    [Fact]
    public async Task GetMyObligationsAsync_NoApartmentMembership_ReturnsEmptyList()
    {
        var result = await _service.GetMyObligationsAsync("stranger");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMyObligationsAsync_ReturnsOnlyCallersApartmentObligations()
    {
        var building = AddManagedBuilding();
        var myApartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        var otherApartment = new Apartment { BuildingId = building.Id, Number = "2", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.AddRange(myApartment, otherApartment);
        var fee = new Fee { BuildingId = building.Id, Title = "Maintenance", Amount = 10, Type = FeeType.Fixed, Frequency = FeeFrequency.Monthly, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = myApartment.Id, UserId = "res1", IsActive = true });
        _context.Obligations.AddRange(
            new Obligation { ApartmentId = myApartment.Id, FeeId = fee.Id, Amount = 10, Status = ObligationStatus.Pending },
            new Obligation { ApartmentId = otherApartment.Id, FeeId = fee.Id, Amount = 10, Status = ObligationStatus.Pending });
        await _context.SaveChangesAsync();

        var result = await _service.GetMyObligationsAsync("res1");

        Assert.Single(result);
        Assert.Equal("1", result[0].ApartmentNumber);
    }

    [Fact]
    public async Task GetMyObligationsAsync_InactiveMembership_ReturnsEmptyList()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "res1", IsActive = false });
        await _context.SaveChangesAsync();

        var result = await _service.GetMyObligationsAsync("res1");

        Assert.Empty(result);
    }

    [Fact]
    public async Task MarkOverdueAsync_DelegatesToObligationStatusService()
    {
        _status.Setup(s => s.MarkOverdueAsync()).ReturnsAsync(4);

        var result = await _service.MarkOverdueAsync();

        Assert.Equal(4, result);
    }
}
