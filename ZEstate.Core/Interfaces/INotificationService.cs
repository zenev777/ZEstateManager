namespace ZEstate.Core.Interfaces
{
    public interface INotificationService
    {
        // Creates an in-app notification for the user and, unless allowEmail is false or the
        // user opted out, queues an email so the caller isn't blocked on SMTP.
        Task NotifyAsync(string userId, string title, string message, string? link = null, bool allowEmail = true);
    }
}
