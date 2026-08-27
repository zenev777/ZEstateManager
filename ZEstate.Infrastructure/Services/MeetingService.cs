using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Meetings;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class MeetingService : IMeetingService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IFileStorage _fileStorage;

    public MeetingService(ApplicationDbContext context, INotificationService notificationService, IFileStorage fileStorage)
    {
        _context = context;
        _notificationService = notificationService;
        _fileStorage = fileStorage;
    }

    public async Task<List<MeetingResponseDto>> GetMeetingsAsync(string userId)
    {
        var building = await GetMyBuildingOrThrowAsync(userId);

        var meetings = await _context.Meetings
            .Where(m => m.BuildingId == building.Id)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync();

        return meetings.Select(ToDto).ToList();
    }

    public async Task<MeetingResponseDto> CreateMeetingAsync(string managerId, CreateMeetingDto dto)
    {
        if (dto.EndDate <= dto.StartDate)
            throw new BadRequestException("Крайната дата трябва да е след началната.");

        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var meeting = new Meeting
        {
            BuildingId = building.Id,
            Title = dto.Title,
            Description = dto.Description,
            Agenda = dto.Agenda,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Location = dto.Location,
            MeetUrl = dto.MeetUrl,
            Status = MeetingStatus.Upcoming
        };

        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        await NotifyBuildingResidentsAsync(building.Id,
            "Ново събрание",
            $"Насрочено е събрание \"{meeting.Title}\" на {meeting.StartDate:dd.MM.yyyy HH:mm}.");

        return ToDto(meeting);
    }

    public async Task<MeetingResponseDto> UpdateMeetingAsync(string managerId, int meetingId, UpdateMeetingDto dto)
    {
        if (dto.EndDate <= dto.StartDate)
            throw new BadRequestException("Крайната дата трябва да е след началната.");

        if (!Enum.TryParse<MeetingStatus>(dto.Status, true, out var status))
            throw new BadRequestException("Невалиден статус. Позволени стойности: Upcoming, Active, Closed.");

        var meeting = await GetOwnedMeetingOrThrowAsync(managerId, meetingId);

        meeting.Title = dto.Title;
        meeting.Description = dto.Description;
        meeting.Agenda = dto.Agenda;
        meeting.StartDate = dto.StartDate;
        meeting.EndDate = dto.EndDate;
        meeting.Location = dto.Location;
        meeting.MeetUrl = dto.MeetUrl;
        meeting.Status = status;

        await _context.SaveChangesAsync();

        await NotifyBuildingResidentsAsync(meeting.BuildingId,
            "Промяна в събрание",
            $"Събрание \"{meeting.Title}\" беше променено. Ново начало: {meeting.StartDate:dd.MM.yyyy HH:mm}.");

        return ToDto(meeting);
    }

    public async Task DeleteMeetingAsync(string managerId, int meetingId)
    {
        var meeting = await GetOwnedMeetingOrThrowAsync(managerId, meetingId);

        _context.Meetings.Remove(meeting);
        await _context.SaveChangesAsync();
    }

    // Generates a placeholder Google Meet-style link (e.g. abc-defg-hij). Doesn't create a
    // real room via Google Calendar/Meet API - that needs OAuth integration out of scope here.
    public string GenerateMeetLink()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        var random = new Random();
        string Segment(int length) => new(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());

        return $"https://meet.google.com/{Segment(3)}-{Segment(4)}-{Segment(3)}";
    }

    public async Task<MeetingMinutesDto> UploadMinutesAsync(
        string managerId, int meetingId, Stream content, string fileName, string? contentType, long length)
    {
        var meeting = await GetOwnedMeetingOrThrowAsync(managerId, meetingId);

        var validationError = DocumentUploadValidation.Validate(length, fileName, contentType);
        if (validationError != null)
            throw new BadRequestException(validationError);

        var storagePath = await _fileStorage.SaveAsync(content, fileName);

        var document = new Document
        {
            BuildingId = meeting.BuildingId,
            MeetingId = meeting.Id,
            FilePath = storagePath,
            FileName = fileName,
            Type = DocumentType.Protocol,
            Access = DocumentAccess.All
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return new MeetingMinutesDto { Id = document.Id, FileName = document.FileName, UploadedAt = document.UploadedAt };
    }

    public async Task<List<MeetingMinutesDto>> GetMinutesAsync(string userId, int meetingId)
    {
        var building = await GetMyBuildingOrThrowAsync(userId);

        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId && m.BuildingId == building.Id);
        if (meeting == null)
            throw new NotFoundException("Събранието не е намерено.");

        var documents = await _context.Documents
            .Where(d => d.MeetingId == meetingId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return documents.Select(d => new MeetingMinutesDto { Id = d.Id, FileName = d.FileName, UploadedAt = d.UploadedAt }).ToList();
    }

    private async Task NotifyBuildingResidentsAsync(int buildingId, string title, string message)
    {
        var recipientUserIds = await _context.ApartmentUsers
            .Where(au => au.IsActive && au.Apartment.BuildingId == buildingId)
            .Select(au => au.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var userId in recipientUserIds)
        {
            await _notificationService.NotifyAsync(userId, title, message, "/dashboard");
        }
    }

    private static MeetingResponseDto ToDto(Meeting meeting) => new()
    {
        Id = meeting.Id,
        Title = meeting.Title,
        Description = meeting.Description,
        Agenda = meeting.Agenda,
        StartDate = meeting.StartDate,
        EndDate = meeting.EndDate,
        Location = meeting.Location,
        MeetUrl = meeting.MeetUrl,
        Status = (int)meeting.Status
    };

    private Task<Building?> GetManagedBuildingAsync(string managerId) =>
        _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == managerId);

    private async Task<Building> GetManagedBuildingOrThrowAsync(string managerId)
    {
        var building = await GetManagedBuildingAsync(managerId);
        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        return building;
    }

    // Manager resolves via Building.ManagerId; any other building member (resident,
    // cashier) resolves via their apartment membership - meetings are visible to all.
    private async Task<Building> GetMyBuildingOrThrowAsync(string userId)
    {
        var managed = await GetManagedBuildingAsync(userId);
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
            throw new NotFoundException("Нямаш сграда.");

        return building;
    }

    private async Task<Meeting> GetOwnedMeetingOrThrowAsync(string managerId, int meetingId)
    {
        var building = await GetManagedBuildingOrThrowAsync(managerId);

        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId && m.BuildingId == building.Id);
        if (meeting == null)
            throw new NotFoundException("Събранието не е намерено.");

        return meeting;
    }
}
