using Microsoft.AspNetCore.Identity;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NotificationEmailQueue _emailQueue;

    public NotificationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        NotificationEmailQueue emailQueue)
    {
        _context = context;
        _userManager = userManager;
        _emailQueue = emailQueue;
    }

    public async Task NotifyAsync(string userId, string title, string message, string? link = null, bool allowEmail = true)
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Link = link
        });

        await _context.SaveChangesAsync();

        if (!allowEmail)
            return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user?.Email != null && user.EmailNotificationsEnabled)
        {
            _emailQueue.Enqueue(new EmailJob(user.Email, title, $"<p>{message}</p>"));
        }
    }
}
