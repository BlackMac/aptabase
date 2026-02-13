namespace Aptabase.Features.Notifications;

public static class NotificationsExtensions
{
    public static void AddNotifications(this IServiceCollection services)
    {
        services.AddHttpClient("Notifications");
        services.AddSingleton<INotificationQueries, NotificationQueries>();
        services.AddSingleton<NotificationChannelFactory>();
        services.AddSingleton<NotificationDispatcher>();
        services.AddHostedService<EventNotificationCronJob>();
        services.AddHostedService<HealthNotificationCronJob>();
        services.AddHostedService<DigestNotificationCronJob>();
    }
}
