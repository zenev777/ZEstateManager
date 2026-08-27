using Moq;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class DocumentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly DocumentService _service;
    private const string ManagerId = "mgr1";
    private const string ResidentId = "res1";

    public DocumentServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new DocumentService(_context, _fileStorage.Object);
    }

    public void Dispose() => _context.Dispose();

    private Building AddManagedBuilding()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        return building;
    }

    private void AddResidentMembership(Building building)
    {
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.SaveChanges();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = ResidentId, Role = ApartmentRole.Resident, IsActive = true });
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetDocumentsAsync_NonManager_OnlySeesAllAccessDocuments()
    {
        var building = AddManagedBuilding();
        AddResidentMembership(building);
        _context.Documents.AddRange(
            new Document { BuildingId = building.Id, FilePath = "p1", FileName = "public.pdf", Type = DocumentType.Protocol, Access = DocumentAccess.All },
            new Document { BuildingId = building.Id, FilePath = "p2", FileName = "private.pdf", Type = DocumentType.Invoice, Access = DocumentAccess.ManagerOnly });
        await _context.SaveChangesAsync();

        var result = await _service.GetDocumentsAsync(ResidentId, null, null, null);

        Assert.Single(result);
        Assert.Equal("public.pdf", result[0].FileName);
    }

    [Fact]
    public async Task GetDocumentsAsync_Manager_SeesAllDocuments()
    {
        var building = AddManagedBuilding();
        _context.Documents.AddRange(
            new Document { BuildingId = building.Id, FilePath = "p1", FileName = "public.pdf", Type = DocumentType.Protocol, Access = DocumentAccess.All },
            new Document { BuildingId = building.Id, FilePath = "p2", FileName = "private.pdf", Type = DocumentType.Invoice, Access = DocumentAccess.ManagerOnly });
        await _context.SaveChangesAsync();

        var result = await _service.GetDocumentsAsync(ManagerId, null, null, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetDocumentsAsync_InvalidTypeFilter_ThrowsBadRequest()
    {
        AddManagedBuilding();

        await Assert.ThrowsAsync<BadRequestException>(() => _service.GetDocumentsAsync(ManagerId, "NotReal", null, null));
    }

    [Fact]
    public async Task UploadDocumentAsync_InvalidCategory_ThrowsBadRequest()
    {
        AddManagedBuilding();
        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.UploadDocumentAsync(ManagerId, stream, "a.pdf", "application/pdf", 3, "NotReal", "All"));
    }

    [Fact]
    public async Task UploadDocumentAsync_InvalidAccess_ThrowsBadRequest()
    {
        AddManagedBuilding();
        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.UploadDocumentAsync(ManagerId, stream, "a.pdf", "application/pdf", 3, "Protocol", "NotReal"));
    }

    [Fact]
    public async Task UploadDocumentAsync_Valid_Persists()
    {
        AddManagedBuilding();
        using var stream = new MemoryStream([1, 2, 3]);
        _fileStorage.Setup(f => f.SaveAsync(stream, "a.pdf", It.IsAny<CancellationToken>())).ReturnsAsync("storage/a.pdf");

        var result = await _service.UploadDocumentAsync(ManagerId, stream, "a.pdf", "application/pdf", 3, "Protocol", "All");

        Assert.Equal("a.pdf", result.FileName);
        Assert.Single(_context.Documents);
    }

    [Fact]
    public async Task DeleteDocumentAsync_NotFound_ThrowsNotFound()
    {
        AddManagedBuilding();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteDocumentAsync(ManagerId, 999));
    }

    [Fact]
    public async Task DeleteDocumentAsync_Valid_RemovesRowAndFile()
    {
        var building = AddManagedBuilding();
        var document = new Document { BuildingId = building.Id, FilePath = "storage/a.pdf", FileName = "a.pdf", Type = DocumentType.Other, Access = DocumentAccess.All };
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        await _service.DeleteDocumentAsync(ManagerId, document.Id);

        Assert.Empty(_context.Documents);
        _fileStorage.Verify(f => f.Delete("storage/a.pdf"), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_ManagerOnlyDocument_NonMemberGetsNotFound()
    {
        var building = AddManagedBuilding();
        var document = new Document { BuildingId = building.Id, FilePath = "storage/a.pdf", FileName = "a.pdf", Type = DocumentType.Invoice, Access = DocumentAccess.ManagerOnly };
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadAsync("stranger", document.Id));
    }

    [Fact]
    public async Task DownloadAsync_ManagerOnlyDocument_MemberGetsNotFound()
    {
        var building = AddManagedBuilding();
        AddResidentMembership(building);
        var document = new Document { BuildingId = building.Id, FilePath = "storage/a.pdf", FileName = "a.pdf", Type = DocumentType.Invoice, Access = DocumentAccess.ManagerOnly };
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadAsync(ResidentId, document.Id));
    }

    [Fact]
    public async Task DownloadAsync_ManagerAlwaysAllowed()
    {
        var building = AddManagedBuilding();
        var document = new Document { BuildingId = building.Id, FilePath = "storage/a.pdf", FileName = "a.pdf", Type = DocumentType.Invoice, Access = DocumentAccess.ManagerOnly };
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        _fileStorage.Setup(f => f.OpenReadAsync("storage/a.pdf", It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream());

        var result = await _service.DownloadAsync(ManagerId, document.Id);

        Assert.Equal("a.pdf", result.FileName);
    }

    [Fact]
    public async Task DownloadAsync_MissingFileInStorage_ThrowsNotFound()
    {
        var building = AddManagedBuilding();
        var document = new Document { BuildingId = building.Id, FilePath = "storage/missing.pdf", FileName = "missing.pdf", Type = DocumentType.Other, Access = DocumentAccess.All };
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        _fileStorage.Setup(f => f.OpenReadAsync("storage/missing.pdf", It.IsAny<CancellationToken>())).ReturnsAsync((Stream?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadAsync(ManagerId, document.Id));
    }
}
