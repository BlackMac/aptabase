namespace Aptabase.Features.Notifications;

public class NotificationDispatcher
{
    private readonly INotificationQueries _queries;
    private readonly NotificationChannelFactory _channelFactory;
    private readonly EnvSettings _env;
    private readonly ILogger<NotificationDispatcher> _logger;

    private const int MaxNotificationsPerHour = 200;

    public NotificationDispatcher(
        INotificationQueries queries,
        NotificationChannelFactory channelFactory,
        EnvSettings env,
        ILogger<NotificationDispatcher> logger)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DispatchAsync(
        NotificationRuleWithChannels rule,
        string title,
        string message,
        string? dedupKey,
        int dedupWindowMinutes,
        CancellationToken ct)
    {
        // Check dedup
        if (!string.IsNullOrEmpty(dedupKey))
        {
            if (await _queries.HasRecentNotification(dedupKey, dedupWindowMinutes))
            {
                _logger.LogDebug("Skipping notification for rule {RuleId} due to dedup key {DedupKey}", rule.Id, dedupKey);
                return;
            }
        }

        // Check per-app rate limit
        var recentCount = await _queries.GetRecentNotificationCount(rule.AppId, 60);
        if (recentCount >= MaxNotificationsPerHour)
        {
            _logger.LogWarning("Rate limit reached for app {AppId}: {Count} notifications in the last hour", rule.AppId, recentCount);
            return;
        }

        // Resolve app icon URL
        var iconUrl = await GetAppIconUrl(rule.AppId);

        // Resolve channels and send
        foreach (var channelId in rule.ChannelIds)
        {
            try
            {
                var channelRow = await _queries.GetChannelById(rule.AppId, channelId);
                if (channelRow == null || !channelRow.Enabled)
                    continue;

                var channel = _channelFactory.Create(channelRow);
                await channel.SendAsync(title, message, ct, iconUrl);

                await _queries.LogNotification(rule.AppId, rule.Id, channelId, message, dedupKey);
                _logger.LogInformation("Notification sent for rule {RuleId} to channel {ChannelId}", rule.Id, channelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for rule {RuleId} to channel {ChannelId}", rule.Id, channelId);
            }
        }
    }

    public async Task SendTestAsync(string appId, NotificationChannelRow channelRow, CancellationToken ct)
    {
        var iconUrl = await GetAppIconUrl(appId);
        var channel = _channelFactory.Create(channelRow);
        await channel.SendAsync("Aptabase Test", "This is a test notification from Aptabase. If you see this, your channel is configured correctly!", ct, iconUrl);
        await _queries.LogNotification(appId, null, channelRow.Id, "Test notification", null);
    }

    private async Task<string?> GetAppIconUrl(string appId)
    {
        var iconPath = await _queries.GetAppIconPath(appId);
        if (string.IsNullOrEmpty(iconPath) || string.IsNullOrEmpty(_env.SelfBaseUrl))
            return null;

        return $"{_env.SelfBaseUrl.TrimEnd('/')}/uploads/{iconPath}";
    }
}
