// MeetingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Meetings;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/meetings")]
[Authorize]
public class MeetingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IFileStorage _fileStorage;

    public MeetingsController(
        ApplicationDbContext context,
        INotificationService notificationService,
        IFileStorage fileStorage)
    {
        _context = context;
        _notificationService = notificationService;
        _fileStorage = fileStorage;
    }

    // GET: Всички събрания на сградата - достъпно за всеки неин член (не само домоуправителя)
    [HttpGet]
    public async Task<IActionResult> GetMeetings()
    {
        var building = await GetMyBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш сграда." });

        var meetings = await _context.Meetings
            .Where(m => m.BuildingId == building.Id)
            .OrderByDescending(m => m.StartDate)
            .Select(m => MeetingResponse(m))
            .ToListAsync();

        return Ok(meetings);
    }

    // POST: Създаване на събрание - известява всички собственици/живущи в сградата
    [HttpPost]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.EndDate <= dto.StartDate)
            return BadRequest(new { message = "Крайната дата трябва да е след началната." });

        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

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

        return Ok(MeetingResponse(meeting));
    }

    // PUT: Редакция на събрание - известява всички собственици/живущи при промяна
    [HttpPut("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> UpdateMeeting(int id, [FromBody] UpdateMeetingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.EndDate <= dto.StartDate)
            return BadRequest(new { message = "Крайната дата трябва да е след началната." });

        if (!Enum.TryParse<MeetingStatus>(dto.Status, true, out var status))
            return BadRequest(new { message = "Невалиден статус. Позволени стойности: Upcoming, Active, Closed." });

        var meeting = await GetOwnedMeetingAsync(id);
        if (meeting == null)
            return NotFound(new { message = "Събранието не е намерено." });

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

        return Ok(MeetingResponse(meeting));
    }

    // DELETE: Изтриване на събрание
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> DeleteMeeting(int id)
    {
        var meeting = await GetOwnedMeetingAsync(id);
        if (meeting == null)
            return NotFound(new { message = "Събранието не е намерено." });

        _context.Meetings.Remove(meeting);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Събранието е изтрито." });
    }

    // POST: Генерира placeholder Google Meet линк във формàта на истински Meet код
    // (напр. abc-defg-hij). Не създава реална стая през Google Calendar/Meet API -
    // това изисква OAuth интеграция извън обхвата тук. Ръчното въвеждане на линк
    // (полето MeetUrl при create/update) си остава напълно валиден път.
    [HttpPost("generate-meet-link")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public IActionResult GenerateMeetLink()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        var random = new Random();
        string Segment(int length) => new(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());

        var link = $"https://meet.google.com/{Segment(3)}-{Segment(4)}-{Segment(3)}";
        return Ok(new { meetUrl = link });
    }

    // POST: Прикачване на протокол към приключило събрание
    [HttpPost("{id:int}/minutes")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    [RequestSizeLimit(DocumentUploadValidation.MaxBytes)]
    public async Task<IActionResult> UploadMinutes(int id, IFormFile file)
    {
        var meeting = await GetOwnedMeetingAsync(id);
        if (meeting == null)
            return NotFound(new { message = "Събранието не е намерено." });

        var validationError = DocumentUploadValidation.Validate(file);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        await using var stream = file.OpenReadStream();
        var storagePath = await _fileStorage.SaveAsync(stream, file.FileName);

        var document = new Document
        {
            BuildingId = meeting.BuildingId,
            MeetingId = meeting.Id,
            FilePath = storagePath,
            FileName = file.FileName,
            Type = DocumentType.Protocol,
            Access = DocumentAccess.All
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return Ok(new { document.Id, document.FileName, document.UploadedAt });
    }

    // GET: Прикачените протоколи на събрание - достъпно за всеки член на сградата
    [HttpGet("{id:int}/minutes")]
    public async Task<IActionResult> GetMinutes(int id)
    {
        var building = await GetMyBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш сграда." });

        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == id && m.BuildingId == building.Id);
        if (meeting == null)
            return NotFound(new { message = "Събранието не е намерено." });

        var documents = await _context.Documents
            .Where(d => d.MeetingId == id)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new { d.Id, d.FileName, d.UploadedAt })
            .ToListAsync();

        return Ok(documents);
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

    private static object MeetingResponse(Meeting meeting) => new
    {
        meeting.Id,
        meeting.Title,
        meeting.Description,
        meeting.Agenda,
        meeting.StartDate,
        meeting.EndDate,
        meeting.Location,
        meeting.MeetUrl,
        meeting.Status
    };

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private Task<Building?> GetManagedBuildingAsync() =>
        _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);

    // Manager resolves via Building.ManagerId; any other building member (resident,
    // cashier) resolves via their apartment membership - meetings are visible to all.
    private async Task<Building?> GetMyBuildingAsync()
    {
        var managed = await GetManagedBuildingAsync();
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

    private async Task<Meeting?> GetOwnedMeetingAsync(int id)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return null;

        return await _context.Meetings.FirstOrDefaultAsync(m => m.Id == id && m.BuildingId == building.Id);
    }
}
