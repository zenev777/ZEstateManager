// NotificationsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Notifications;
using ZEstate.Core.Interfaces;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IUserNotificationService _notificationService;

    public NotificationsController(IUserNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    // GET: Последните известия на текущия потребител
    [HttpGet]
    public async Task<IActionResult> GetMyNotifications() =>
        Ok(await _notificationService.GetMyNotificationsAsync(CurrentUserId));

    // GET: Брой непрочетени известия (за bell икона)
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount() =>
        Ok(new { count = await _notificationService.GetUnreadCountAsync(CurrentUserId) });

    // POST: Маркиране на известие като прочетено
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _notificationService.MarkAsReadAsync(CurrentUserId, id);
        return Ok(new { message = "Известието е маркирано като прочетено." });
    }

    // POST: Маркиране на всички известия като прочетени
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        return Ok(new { message = "Всички известия са маркирани като прочетени." });
    }

    // GET: Текущите настройки за имейл известия
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences() =>
        Ok(await _notificationService.GetPreferencesAsync(CurrentUserId));

    // PUT: Смяна на настройките за имейл известия
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] NotificationPreferencesDto dto) =>
        Ok(await _notificationService.UpdatePreferencesAsync(CurrentUserId, dto));

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
