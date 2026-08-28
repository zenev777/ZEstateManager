using Moq;
using ZEstate.Core.DTOs.Repairs;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class RepairServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly RepairService _service;
    private const string ManagerId = "mgr1";

    public RepairServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new RepairService(_context, _fileStorage.Object);
    }

    public void Dispose() => _context.Dispose();

    private Building AddManagedBuilding()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        return building;
    }

    private Repair AddRepair(Building building, RepairStatus status = RepairStatus.Planned, decimal budget = 1000, decimal? actualCost = null)
    {
        var repair = new Repair { BuildingId = building.Id, Title = "R", Budget = budget, ActualCost = actualCost, Status = status };
        _context.Repairs.Add(repair);
        _context.SaveChanges();
        return repair;
    }

    [Fact]
    public async Task GetRepairsAsync_NonMember_ThrowsNotFound()
    {
        AddManagedBuilding();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetRepairsAsync("stranger"));
    }

    [Fact]
    public async Task GetRepairsAsync_ResidentWithoutManagerRights_SeesRepairsReadOnly()
    {
        var building = AddManagedBuilding();
        AddRepair(building);
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "res1", IsActive = true });
        await _context.SaveChangesAsync();

        var result = await _service.GetRepairsAsync("res1");

        Assert.Single(result);
    }

    [Fact]
    public async Task CreateRepairAsync_StartsAsPlanned()
    {
        AddManagedBuilding();
        var result = await _service.CreateRepairAsync(ManagerId, new CreateRepairDto { Title = "Roof", Budget = 500 });

        Assert.Equal((int)RepairStatus.Planned, result.Status);
    }

    [Fact]
    public async Task UpdateRepairAsync_InvalidStatus_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building);

        var dto = new UpdateRepairDto { Title = "R", Budget = 100, Status = "NotReal" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateRepairAsync(ManagerId, repair.Id, dto));
    }

    [Fact]
    public async Task DeleteRepairAsync_CostsAllocated_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building);
        _context.Fees.Add(new Fee { BuildingId = building.Id, RepairId = repair.Id, Title = "R", Amount = 100, Type = FeeType.Repair, Frequency = FeeFrequency.OneTime, DateFrom = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => _service.DeleteRepairAsync(ManagerId, repair.Id));
    }

    [Fact]
    public async Task AllocateCostsAsync_StillPlanned_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building, RepairStatus.Planned);
        _context.Apartments.Add(new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 100, Budget = 0 });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.AllocateCostsAsync(ManagerId, repair.Id, new AllocateRepairCostsDto()));
    }

    [Fact]
    public async Task AllocateCostsAsync_AlreadyAllocated_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building, RepairStatus.Completed, budget: 100);
        _context.Fees.Add(new Fee { BuildingId = building.Id, RepairId = repair.Id, Title = "R", Amount = 100, Type = FeeType.Repair, Frequency = FeeFrequency.OneTime, DateFrom = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.AllocateCostsAsync(ManagerId, repair.Id, new AllocateRepairCostsDto()));
    }

    [Fact]
    public async Task AllocateCostsAsync_NoApartments_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building, RepairStatus.Completed, budget: 100);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.AllocateCostsAsync(ManagerId, repair.Id, new AllocateRepairCostsDto()));
    }

    [Fact]
    public async Task AllocateCostsAsync_ProportionalByIdealParts_LastApartmentAbsorbsRounding()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building, RepairStatus.Completed, budget: 100);
        _context.Apartments.AddRange(
            new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 33.33m, Budget = 0 },
            new Apartment { BuildingId = building.Id, Number = "2", Floor = 1, IdealParts = 33.33m, Budget = 0 },
            new Apartment { BuildingId = building.Id, Number = "3", Floor = 1, IdealParts = 33.34m, Budget = 0 });
        await _context.SaveChangesAsync();

        var result = await _service.AllocateCostsAsync(ManagerId, repair.Id, new AllocateRepairCostsDto());

        Assert.Equal(3, result.ObligationsCreated);
        Assert.Equal(100, result.TotalCost);
        var totalAllocated = _context.Obligations.Where(o => o.FeeId == result.FeeId).Sum(o => o.Amount);
        Assert.Equal(100, totalAllocated);
    }

    [Fact]
    public async Task AllocateCostsAsync_ManualAllocationMismatch_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building, RepairStatus.Completed, budget: 100);
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 100, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        var dto = new AllocateRepairCostsDto
        {
            ManualAllocations = [new ManualAllocationEntryDto { ApartmentId = apartment.Id, Amount = 50 }]
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _service.AllocateCostsAsync(ManagerId, repair.Id, dto));
    }

    [Fact]
    public async Task AllocateCostsAsync_ManualAllocationUnknownApartment_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building, RepairStatus.Completed, budget: 100);
        _context.Apartments.Add(new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 100, Budget = 0 });
        await _context.SaveChangesAsync();

        var dto = new AllocateRepairCostsDto
        {
            ManualAllocations = [new ManualAllocationEntryDto { ApartmentId = 9999, Amount = 100 }]
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _service.AllocateCostsAsync(ManagerId, repair.Id, dto));
    }

    [Fact]
    public async Task AllocateCostsAsync_ManualAllocationExact_Succeeds()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building, RepairStatus.Completed, budget: 100);
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 100, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        var dto = new AllocateRepairCostsDto
        {
            ManualAllocations = [new ManualAllocationEntryDto { ApartmentId = apartment.Id, Amount = 100 }]
        };

        var result = await _service.AllocateCostsAsync(ManagerId, repair.Id, dto);

        Assert.Equal(1, result.ObligationsCreated);
    }

    [Fact]
    public async Task AllocateCostsAsync_UsesActualCostOverBudgetWhenSet()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building, RepairStatus.Completed, budget: 100, actualCost: 150);
        _context.Apartments.Add(new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 100, Budget = 0 });
        await _context.SaveChangesAsync();

        var result = await _service.AllocateCostsAsync(ManagerId, repair.Id, new AllocateRepairCostsDto());

        Assert.Equal(150, result.TotalCost);
    }

    [Fact]
    public async Task UploadDocumentAsync_EmptyFile_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building);
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.UploadDocumentAsync(ManagerId, repair.Id, stream, "invoice.pdf", "application/pdf", 0));
    }

    [Fact]
    public async Task UploadDocumentAsync_DisallowedType_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building);
        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.UploadDocumentAsync(ManagerId, repair.Id, stream, "invoice.exe", "application/x-msdownload", 3));
    }

    [Fact]
    public async Task UploadDocumentAsync_Valid_SavesDocumentAsManagerOnlyInvoice()
    {
        var building = AddManagedBuilding();
        var repair = AddRepair(building);
        using var stream = new MemoryStream([1, 2, 3]);
        _fileStorage.Setup(f => f.SaveAsync(stream, "invoice.pdf", It.IsAny<CancellationToken>())).ReturnsAsync("storage/invoice.pdf");

        var result = await _service.UploadDocumentAsync(ManagerId, repair.Id, stream, "invoice.pdf", "application/pdf", 3);

        Assert.Equal("invoice.pdf", result.FileName);
        var doc = _context.Documents.Single();
        Assert.Equal(DocumentAccess.ManagerOnly, doc.Access);
        Assert.Equal(DocumentType.Invoice, doc.Type);
        Assert.Equal(repair.Id, doc.RepairId);
    }

    [Fact]
    public async Task GetDocumentsAsync_RepairNotFound_ThrowsNotFound()
    {
        AddManagedBuilding();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetDocumentsAsync(ManagerId, 999));
    }
}
