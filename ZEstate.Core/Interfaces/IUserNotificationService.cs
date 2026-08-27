using ZEstate.Core.DTOs.Notifications;

namespace ZEstate.Core.Interfaces
{
    // Query/manage the current user's own notifications (list, unread count, mark
    // read, email preference). Not to be confused with INotificationService, which
    // creates a notification for an arbitrary user from elsewhere in the app.
    public interface IUserNotificationService
    {
        Task<List<NotificationResponseDto>> GetMyNotificationsAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(string userId, int notificationId);
        Task MarkAllAsReadAsync(string userId);
        Task<NotificationPreferencesDto> GetPreferencesAsync(string userId);
        Task<NotificationPreferencesDto> UpdatePreferencesAsync(string userId, NotificationPreferencesDto dto);
    }
}
