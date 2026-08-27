using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Notifications;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Services;

public class UserNotificationService : IUserNotificationService
{
    private const int RecentLimit = 50;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserNotificationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<NotificationResponseDto>> GetMyNotificationsAsync(string userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(RecentLimit)
            .ToListAsync();

        return notifications.Select(n => new NotificationResponseDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Link = n.Link,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    public Task<int> GetUnreadCountAsync(string userId) =>
        _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkAsReadAsync(string userId, int notificationId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            throw new NotFoundException("Известието не е намерено.");

        notification.IsRead = true;
        await _context.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
    }

    public async Task<NotificationPreferencesDto> GetPreferencesAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("Потребителят не е намерен.");

        return new NotificationPreferencesDto { EmailEnabled = user.EmailNotificationsEnabled };
    }

    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(string userId, NotificationPreferencesDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("Потребителят не е намерен.");

        user.EmailNotificationsEnabled = dto.EmailEnabled;
        await _userManager.UpdateAsync(user);

        return new NotificationPreferencesDto { EmailEnabled = user.EmailNotificationsEnabled };
    }
}
