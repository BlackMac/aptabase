using System.Text.Json;
using Aptabase.Features.Stats;
using Sgbj.Cron;

namespace Aptabase.Features.Notifications;

public class EventNotificationCronJob : BackgroundService
{
    private readonly INotificationQueries _queries;
    private readonly IQueryClient _queryClient;
    private readonly NotificationDispatcher _dispatcher;
    private readonly ILogger _logger;

    public EventNotificationCronJob(
        INotificationQueries queries,
        IQueryClient queryClient,
        NotificationDispatcher dispatcher,
        ILogger<EventNotificationCronJob> logger)
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
                _logger.LogInformation("EventNotificationCronJob is starting.");

                using var timer = new CronTimer("*/5 * * * *", TimeZoneInfo.Utc);

                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    _logger.LogInformation("EventNotificationCronJob tick.");

                    try
                    {
                        await ProcessEventPushRules(cancellationToken);
                        await ProcessThresholdRules(cancellationToken);
                        await ProcessNewValueRules("new_event_name", "event_name", cancellationToken);
                        await ProcessNewValueRules("new_app_version", "app_version", cancellationToken);
                        await ProcessNewValueRules("new_country", "country_code", cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "EventNotificationCronJob failed during tick processing.");
                    }

                    _logger.LogInformation("EventNotificationCronJob tick complete.");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("EventNotificationCronJob stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventNotificationCronJob crashed.");
            }
        }
    }

    private async Task ProcessEventPushRules(CancellationToken ct)
    {
        var rules = (await _queries.GetAllEnabledRulesByType("event_push")).ToList();
        if (rules.Count == 0) return;

        var appIds = rules.Select(r => r.AppId).Distinct().ToArray();
        var since = DateTime.UtcNow.AddMinutes(-5);

        var eventCounts = await _queryClient.NamedQueryAsync<EventCountResult>(
            "notification_event_counts__v1",
            new { app_ids = appIds, since },
            ct);

        var countsByApp = eventCounts.GroupBy(e => e.AppId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.EventName, e => e.Count));

        foreach (var rule in rules)
        {
            try
            {
                if (!countsByApp.TryGetValue(rule.AppId, out var appCounts))
                    continue;

                var config = JsonDocument.Parse(rule.ConfigJson).RootElement;
                var eventNames = config.GetProperty("event_names").EnumerateArray()
                    .Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();
                var dedup = config.TryGetProperty("dedup", out var dedupProp) && dedupProp.GetBoolean();
                var dedupWindow = config.TryGetProperty("dedup_window_minutes", out var windowProp) ? windowProp.GetInt32() : 60;

                foreach (var eventName in eventNames)
                {
                    if (!appCounts.TryGetValue(eventName, out var count) || count == 0)
                        continue;

                    var dedupKey = dedup ? $"event_push:{rule.Id}:{eventName}" : null;
                    await _dispatcher.DispatchAsync(
                        rule,
                        $"Event: {eventName}",
                        $"{count} occurrence(s) of '{eventName}' in the last 5 minutes.",
                        dedupKey,
                        dedupWindow,
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process event_push rule {RuleId}", rule.Id);
            }
        }
    }

    private async Task ProcessThresholdRules(CancellationToken ct)
    {
        var rules = (await _queries.GetAllEnabledRulesByType("threshold")).ToList();
        if (rules.Count == 0) return;

        var appIds = rules.Select(r => r.AppId).Distinct().ToArray();

        foreach (var rule in rules)
        {
            try
            {
                var config = JsonDocument.Parse(rule.ConfigJson).RootElement;
                var eventName = config.GetProperty("event_name").GetString() ?? "";
                var threshold = config.GetProperty("threshold").GetInt64();
                var period = config.TryGetProperty("period", out var periodProp) ? periodProp.GetString() ?? "day" : "day";

                var since = period == "hour" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.Date;

                var eventCounts = await _queryClient.NamedQueryAsync<EventCountResult>(
                    "notification_event_counts__v1",
                    new { app_ids = new[] { rule.AppId }, since },
                    ct);

                var count = eventCounts.Where(e => e.EventName == eventName).Sum(e => e.Count);
                if (count <= threshold)
                    continue;

                var dedupKey = $"threshold:{rule.Id}:{(period == "hour" ? DateTime.UtcNow.ToString("yyyy-MM-dd-HH") : DateTime.UtcNow.ToString("yyyy-MM-dd"))}";
                await _dispatcher.DispatchAsync(
                    rule,
                    $"Threshold Alert: {eventName}",
                    $"Event '{eventName}' has reached {count} occurrences (threshold: {threshold}) in the current {period}.",
                    dedupKey,
                    period == "hour" ? 60 : 1440,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process threshold rule {RuleId}", rule.Id);
            }
        }
    }

    private async Task ProcessNewValueRules(string ruleType, string columnName, CancellationToken ct)
    {
        var rules = (await _queries.GetAllEnabledRulesByType(ruleType)).ToList();
        if (rules.Count == 0) return;

        var appIds = rules.Select(r => r.AppId).Distinct().ToArray();
        var since = DateTime.UtcNow.AddMinutes(-5);

        var distinctValues = await _queryClient.NamedQueryAsync<DistinctValueResult>(
            "notification_distinct_values__v1",
            new { app_ids = appIds, column_name = columnName, since },
            ct);

        var valuesByApp = distinctValues.GroupBy(v => v.AppId)
            .ToDictionary(g => g.Key, g => g.Select(v => v.Value).ToHashSet());

        // Map rule_type to value_type for known values
        var valueType = ruleType switch
        {
            "new_event_name" => "event_name",
            "new_app_version" => "app_version",
            "new_country" => "country_code",
            _ => columnName
        };

        foreach (var rule in rules)
        {
            try
            {
                if (!valuesByApp.TryGetValue(rule.AppId, out var currentValues))
                    continue;

                var knownValues = (await _queries.GetKnownValues(rule.AppId, valueType))
                    .Select(k => k.Value).ToHashSet();

                var newValues = currentValues.Except(knownValues).ToList();
                if (newValues.Count == 0)
                    continue;

                // Track the new values
                await _queries.UpsertKnownValues(rule.AppId, valueType, newValues);

                var label = ruleType switch
                {
                    "new_event_name" => "event name",
                    "new_app_version" => "app version",
                    "new_country" => "country",
                    _ => "value"
                };

                foreach (var newValue in newValues)
                {
                    var dedupKey = $"{ruleType}:{rule.AppId}:{newValue}";
                    await _dispatcher.DispatchAsync(
                        rule,
                        $"New {label} detected",
                        $"A new {label} was detected: '{newValue}'",
                        dedupKey,
                        1440,
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {RuleType} rule {RuleId}", ruleType, rule.Id);
            }
        }
    }
}
