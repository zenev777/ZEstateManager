// MeetingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Meetings;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Services;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/meetings")]
[Authorize]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    public MeetingsController(IMeetingService meetingService)
    {
        _meetingService = meetingService;
    }

    // GET: Всички събрания на сградата - достъпно за всеки неин член (не само домоуправителя)
    [HttpGet]
    public async Task<IActionResult> GetMeetings() =>
        Ok(await _meetingService.GetMeetingsAsync(CurrentUserId));

    // POST: Създаване на събрание - известява всички собственици/живущи в сградата
    [HttpPost]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _meetingService.CreateMeetingAsync(CurrentUserId, dto));
    }

    // PUT: Редакция на събрание - известява всички собственици/живущи при промяна
    [HttpPut("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> UpdateMeeting(int id, [FromBody] UpdateMeetingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _meetingService.UpdateMeetingAsync(CurrentUserId, id, dto));
    }

    // DELETE: Изтриване на събрание
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> DeleteMeeting(int id)
    {
        await _meetingService.DeleteMeetingAsync(CurrentUserId, id);
        return Ok(new { message = "Събранието е изтрито." });
    }

    // POST: Генерира placeholder Google Meet линк във формàта на истински Meet код
    // (напр. abc-defg-hij). Не създава реална стая през Google Calendar/Meet API -
    // това изисква OAuth интеграция извън обхвата тук. Ръчното въвеждане на линк
    // (полето MeetUrl при create/update) си остава напълно валиден път.
    [HttpPost("generate-meet-link")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public IActionResult GenerateMeetLink() =>
        Ok(new { meetUrl = _meetingService.GenerateMeetLink() });

    // POST: Прикачване на протокол към приключило събрание
    [HttpPost("{id:int}/minutes")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    [RequestSizeLimit(DocumentUploadValidation.MaxBytes)]
    public async Task<IActionResult> UploadMinutes(int id, IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        var result = await _meetingService.UploadMinutesAsync(CurrentUserId, id, stream, file.FileName, file.ContentType, file.Length);

        return Ok(result);
    }

    // GET: Прикачените протоколи на събрание - достъпно за всеки член на сградата
    [HttpGet("{id:int}/minutes")]
    public async Task<IActionResult> GetMinutes(int id) =>
        Ok(await _meetingService.GetMinutesAsync(CurrentUserId, id));

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
