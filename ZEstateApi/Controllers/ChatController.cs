// ChatController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;
using ZEstateApi.Hubs;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private const int HistoryLimit = 50;

    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hub;

    public ChatController(ApplicationDbContext context, IHubContext<ChatHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    // GET: Последните съобщения на общия канал на сградата - зарежда се при отваряне на чата
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages()
    {
        var buildingId = await GetMyBuildingIdAsync();
        if (buildingId == null)
            return NotFound(new { message = "Нямаш сграда." });

        var messages = await _context.ChatMessages
            .Where(m => m.BuildingId == buildingId)
            .Include(m => m.User)
            .OrderByDescending(m => m.SentAt)
            .Take(HistoryLimit)
            .OrderBy(m => m.SentAt)
            .Select(m => MessageResponse(m))
            .ToListAsync();

        return Ok(messages);
    }

    // POST: Изпращане на съобщение в общия канал - записва се и се разпраща на всички свързани клиенти
    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendChatMessageDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var buildingId = await GetMyBuildingIdAsync();
        if (buildingId == null)
            return NotFound(new { message = "Нямаш сграда." });

        var userId = CurrentUserId;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return Unauthorized();

        var chatMessage = new ChatMessage
        {
            BuildingId = buildingId.Value,
            UserId = userId,
            Message = dto.Message.Trim()
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        var response = new
        {
            chatMessage.Id,
            chatMessage.Message,
            chatMessage.SentAt,
            senderId = userId,
            senderName = user.Name
        };

        await _hub.Clients.Group(ChatHub.GroupName(buildingId.Value)).SendAsync("ReceiveMessage", response);

        return Ok(response);
    }

    // DELETE: Изтриване на неподходящо съобщение - само домоуправителят/администраторът
    [HttpDelete("messages/{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var buildingId = await GetMyBuildingIdAsync();
        if (buildingId == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var message = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == id && m.BuildingId == buildingId);
        if (message == null)
            return NotFound(new { message = "Съобщението не е намерено." });

        _context.ChatMessages.Remove(message);
        await _context.SaveChangesAsync();

        await _hub.Clients.Group(ChatHub.GroupName(buildingId.Value)).SendAsync("MessageDeleted", id);

        return Ok(new { message = "Съобщението е изтрито." });
    }

    private static object MessageResponse(ChatMessage m) => new
    {
        m.Id,
        m.Message,
        m.SentAt,
        senderId = m.UserId,
        senderName = m.User.Name
    };

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<int?> GetMyBuildingIdAsync()
    {
        var managed = await _context.Buildings
            .Where(b => b.ManagerId == CurrentUserId)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync();

        if (managed != null)
            return managed;

        return await _context.ApartmentUsers
            .Where(au => au.UserId == CurrentUserId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();
    }
}

public class SendChatMessageDto
{
    [Required]
    [MaxLength(ChatMessage.MessageMaxLength)]
    public string Message { get; set; } = string.Empty;
}
