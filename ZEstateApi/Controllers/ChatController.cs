// ChatController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;
using ZEstateApi.Hubs;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _hub;

    public ChatController(IChatService chatService, IHubContext<ChatHub> hub)
    {
        _chatService = chatService;
        _hub = hub;
    }

    // GET: Последните съобщения на общия канал на сградата - зарежда се при отваряне на чата
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages() =>
        Ok(await _chatService.GetMessagesAsync(CurrentUserId));

    // POST: Изпращане на съобщение в общия канал - записва се и се разпраща на всички свързани клиенти
    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendChatMessageDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _chatService.SendMessageAsync(CurrentUserId, dto.Message);

        await _hub.Clients.Group(ChatHub.GroupName(result.BuildingId)).SendAsync("ReceiveMessage", result.Message);

        return Ok(result.Message);
    }

    // DELETE: Изтриване на неподходящо съобщение - само домоуправителят/администраторът
    [HttpDelete("messages/{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var result = await _chatService.DeleteMessageAsync(CurrentUserId, id);

        await _hub.Clients.Group(ChatHub.GroupName(result.BuildingId)).SendAsync("MessageDeleted", id);

        return Ok(new { message = "Съобщението е изтрито." });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

public class SendChatMessageDto
{
    [Required]
    [MaxLength(ChatMessage.MessageMaxLength)]
    public string Message { get; set; } = string.Empty;
}
