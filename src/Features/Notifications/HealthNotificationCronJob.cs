using System.Text.Json;
using Aptabase.Features.Stats;
using Sgbj.Cron;

namespace Aptabase.Features.Notifications;

public class HealthNotificationCronJob : BackgroundService
{
    private readonly INotificationQueries _queries;
    private readonly IQueryClient _queryClient;
    private readonly NotificationDispatcher _dispatcher;
    private readonly ILogger _logger;

    public HealthNotificationCronJob(
        INotificationQueries queries,
        IQueryClient queryClient,
        NotificationDispatcher dispatcher,
        ILogger<HealthNotificationCronJob> logger)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("HealthNotificationCronJob is starting.");

                using var timer = new CronTimer("15,45 * * * *", TimeZoneInfo.Utc);

                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    _logger.LogInformation("HealthNotificationCronJob tick.");

                    try
                    {
                        await ProcessDeadAppRules(cancellationToken);
                        await ProcessVolumeAnomalyRules(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "HealthNotificationCronJob failed during tick processing.");
                    }

                    _logger.LogInformation("HealthNotificationCronJob tick complete.");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("HealthNotificationCronJob stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HealthNotificationCronJob crashed.");
            }
        }
    }

    private async Task ProcessDeadAppRules(CancellationToken ct)
    {
        var rules = (await _queries.GetAllEnabledRulesByType("dead_app")).ToList();
        if (rules.Count == 0) return;

        var appIds = rules.Select(r => r.AppId).Distinct().ToArray();

        var lastEvents = await _queryClient.NamedQueryAsync<LastEventResult>(
            "notification_last_event__v1",
            new { app_ids = appIds },
            ct);

        var lastEventByApp = lastEvents.ToDictionary(e => e.AppId, e => e.LastTimestamp);

        foreach (var rule in rules)
        {
            try
            {
                var config = JsonDocument.Parse(rule.ConfigJson).RootElement;
                var hours = config.TryGetProperty("hours", out var hoursProp) ? hoursProp.GetInt32() : 24;

                if (!lastEventByApp.TryGetValue(rule.AppId, out var lastTimestamp))
                    continue; // No events at all — app might be new

                var timeSinceLastEvent = DateTime.UtcNow - lastTimestamp;
                if (timeSinceLastEvent.TotalHours < hours)
                    continue;

                var dedupKey = $"dead_app:{rule.Id}:{DateTime.UtcNow:yyyy-MM-dd}";
                await _dispatcher.DispatchAsync(
                    rule,
                    "Dead App Alert",
                    $"No events received for {timeSinceLastEvent.TotalHours:F1} hours (threshold: {hours}h). Last event was at {lastTimestamp:u}.",
                    dedupKey,
                    1440,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process dead_app rule {RuleId}", rule.Id);
            }
        }
    }

    private async Task ProcessVolumeAnomalyRules(CancellationToken ct)
    {
        var rules = (await _queries.GetAllEnabledRulesByType("volume_anomaly")).ToList();
        if (rules.Count == 0) return;

        var appIds = rules.Select(r => r.AppId).Distinct().ToArray();
        var since = DateTime.UtcNow.AddDays(-30);

        var dailyVolumes = await _queryClient.NamedQueryAsync<DailyVolumeResult>(
            "notification_daily_volume__v1",
            new { app_ids = appIds, since },
            ct);

        var volumesByApp = dailyVolumes.GroupBy(v => v.AppId)
            .ToDictionary(g => g.Key, g => g.OrderBy(v => v.Date).ToList());

        foreach (var rule in rules)
        {
            try
            {
                if (!volumesByApp.TryGetValue(rule.AppId, out var volumes) || volumes.Count < 7)
                    continue; // Need at least a week of data

                var config = JsonDocument.Parse(rule.ConfigJson).RootElement;
                var sensitivity = config.TryGetProperty("sensitivity", out var sensProp) ? sensProp.GetString() ?? "medium" : "medium";

                var sigmaMultiplier = sensitivity switch
                {
                    "low" => 3.0,
                    "medium" => 2.0,
                    "high" => 1.5,
                    _ => 2.0
                };

                // Calculate rolling stats (exclude today)
                var historicalCounts = volumes.Where(v => v.Date < DateTime.UtcNow.Date)
                    .Select(v => (double)v.Count).ToList();

                if (historicalCounts.Count < 7) continue;

                var mean = historicalCounts.Average();
                var stddev = Math.Sqrt(historicalCounts.Select(c => Math.Pow(c - mean, 2)).Average());

                // Check today's count
                var todayCount = volumes.Where(v => v.Date == DateTime.UtcNow.Date)
                    .Select(v => (double)v.Count).FirstOrDefault();

                if (stddev == 0) continue; // No variance

                var zScore = Math.Abs(todayCount - mean) / stddev;
                if (zScore < sigmaMultiplier) continue;

                var direction = todayCount > mean ? "spike" : "drop";
                var dedupKey = $"volume_anomaly:{rule.Id}:{DateTime.UtcNow:yyyy-MM-dd}";

                await _dispatcher.DispatchAsync(
                    rule,
                    $"Volume {direction} detected",
                    $"Today's event count ({todayCount:F0}) is {zScore:F1}σ {direction} from the 30-day average ({mean:F0}). Sensitivity: {sensitivity}.",
                    dedupKey,
                    1440,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process volume_anomaly rule {RuleId}", rule.Id);
            }
        }
    }
}
