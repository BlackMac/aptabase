namespace Aptabase.Features.Notifications;

public interface INotificationChannel
{
    Task SendAsync(string title, string message, CancellationToken ct);
}
