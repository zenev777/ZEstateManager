using Moq;
using ZEstate.Core.DTOs.Meetings;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class MeetingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IFileStorage> _fileStorage = new();
    private readonly MeetingService _service;
    private const string ManagerId = "mgr1";

    public MeetingServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new MeetingService(_context, _notifications.Object, _fileStorage.Object);
    }

    public void Dispose() => _context.Dispose();

    private Building AddManagedBuilding()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        return building;
    }

    private static CreateMeetingDto ValidCreateDto() => new()
    {
        Title = "Annual meeting",
        StartDate = DateTime.UtcNow.AddDays(1),
        EndDate = DateTime.UtcNow.AddDays(1).AddHours(2)
    };

    [Fact]
    public async Task CreateMeetingAsync_EndBeforeStart_ThrowsBadRequest()
    {
        AddManagedBuilding();
        var dto = ValidCreateDto();
        dto.EndDate = dto.StartDate.AddHours(-1);

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateMeetingAsync(ManagerId, dto));
    }

    [Fact]
    public async Task CreateMeetingAsync_Valid_NotifiesActiveResidentsOnly()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();
        _context.ApartmentUsers.AddRange(
            new ApartmentUser { ApartmentId = apartment.Id, UserId = "res1", IsActive = true },
            new ApartmentUser { ApartmentId = apartment.Id, UserId = "res2", IsActive = false });
        await _context.SaveChangesAsync();

        var result = await _service.CreateMeetingAsync(ManagerId, ValidCreateDto());

        Assert.Equal((int)MeetingStatus.Upcoming, result.Status);
        _notifications.Verify(n => n.NotifyAsync("res1", It.IsAny<string>(), It.IsAny<string>(), "/dashboard", true), Times.Once);
        _notifications.Verify(n => n.NotifyAsync("res2", It.IsAny<string>(), It.IsAny<string>(), "/dashboard", true), Times.Never);
    }

    [Fact]
    public async Task UpdateMeetingAsync_InvalidStatus_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var meeting = new Meeting { BuildingId = building.Id, Title = "M", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddHours(1) };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var dto = new UpdateMeetingDto { Title = "M", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddHours(1), Status = "NotReal" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateMeetingAsync(ManagerId, meeting.Id, dto));
    }

    [Fact]
    public async Task DeleteMeetingAsync_NotFound_ThrowsNotFound()
    {
        AddManagedBuilding();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteMeetingAsync(ManagerId, 999));
    }

    [Fact]
    public void GenerateMeetLink_ProducesGoogleMeetShapedUrl()
    {
        var link = _service.GenerateMeetLink();

        Assert.Matches(@"^https://meet\.google\.com/[a-z]{3}-[a-z]{4}-[a-z]{3}$", link);
    }

    [Fact]
    public async Task UploadMinutesAsync_InvalidFile_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var meeting = new Meeting { BuildingId = building.Id, Title = "M", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddHours(1) };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.UploadMinutesAsync(ManagerId, meeting.Id, stream, "minutes.pdf", "application/pdf", 0));
    }

    [Fact]
    public async Task UploadMinutesAsync_Valid_SavesAsAllAccessProtocol()
    {
        var building = AddManagedBuilding();
        var meeting = new Meeting { BuildingId = building.Id, Title = "M", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddHours(1) };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();
        using var stream = new MemoryStream([1, 2, 3]);
        _fileStorage.Setup(f => f.SaveAsync(stream, "minutes.pdf", It.IsAny<CancellationToken>())).ReturnsAsync("storage/minutes.pdf");

        await _service.UploadMinutesAsync(ManagerId, meeting.Id, stream, "minutes.pdf", "application/pdf", 3);

        var doc = _context.Documents.Single();
        Assert.Equal(DocumentType.Protocol, doc.Type);
        Assert.Equal(DocumentAccess.All, doc.Access);
        Assert.Equal(meeting.Id, doc.MeetingId);
    }

    [Fact]
    public async Task GetMinutesAsync_MeetingNotFound_ThrowsNotFound()
    {
        AddManagedBuilding();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetMinutesAsync(ManagerId, 999));
    }
}
