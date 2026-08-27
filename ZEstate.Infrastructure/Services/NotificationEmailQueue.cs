using System.Threading.Channels;

namespace ZEstate.Infrastructure.Services;

public record EmailJob(string ToEmail, string Subject, string HtmlBody);

// In-process queue so NotificationService can hand off emails without waiting on SMTP;
// NotificationEmailDispatcher (a BackgroundService) drains it.
public class NotificationEmailQueue
{
    private readonly Channel<EmailJob> _channel = Channel.CreateUnbounded<EmailJob>();

    public ChannelReader<EmailJob> Reader => _channel.Reader;

    public void Enqueue(EmailJob job) => _channel.Writer.TryWrite(job);
}
