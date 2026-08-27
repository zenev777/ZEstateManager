// NotificationsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Notifications;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.IdentityModels;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private const int RecentLimit = 50;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Последните известия на текущия потребител
    [HttpGet]
    public async Task<IActionResult> GetMyNotifications()
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == CurrentUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(RecentLimit)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Link,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    // GET: Брой непрочетени известия (за bell икона)
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _context.Notifications
            .CountAsync(n => n.UserId == CurrentUserId && !n.IsRead);

        return Ok(new { count });
    }

    // POST: Маркиране на известие като прочетено
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);

        if (notification == null)
            return NotFound(new { message = "Известието не е намерено." });

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Известието е маркирано като прочетено." });
    }

    // POST: Маркиране на всички известия като прочетени
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _context.Notifications
            .Where(n => n.UserId == CurrentUserId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));

        return Ok(new { message = "Всички известия са маркирани като прочетени." });
    }

    // GET: Текущите настройки за имейл известия
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId);
        if (user == null)
            return NotFound();

        return Ok(new NotificationPreferencesDto { EmailEnabled = user.EmailNotificationsEnabled });
    }

    // PUT: Смяна на настройките за имейл известия
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] NotificationPreferencesDto dto)
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId);
        if (user == null)
            return NotFound();

        user.EmailNotificationsEnabled = dto.EmailEnabled;
        await _userManager.UpdateAsync(user);

        return Ok(new NotificationPreferencesDto { EmailEnabled = user.EmailNotificationsEnabled });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
