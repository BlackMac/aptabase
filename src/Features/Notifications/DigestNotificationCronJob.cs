using System.Text;
using System.Text.Json;
using Aptabase.Features.Stats;
using Sgbj.Cron;

namespace Aptabase.Features.Notifications;

public class DigestNotificationCronJob : BackgroundService
{
    private readonly INotificationQueries _queries;
    private readonly IQueryClient _queryClient;
    private readonly NotificationDispatcher _dispatcher;
    private readonly ILogger _logger;

    public DigestNotificationCronJob(
        INotificationQueries queries,
        IQueryClient queryClient,
        NotificationDispatcher dispatcher,
        ILogger<DigestNotificationCronJob> logger)
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
                _logger.LogInformation("DigestNotificationCronJob is starting.");

                using var timer = new CronTimer("0 8 * * *", TimeZoneInfo.Utc);

                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    _logger.LogInformation("DigestNotificationCronJob tick.");

                    try
                    {
                        await ProcessDigestRules(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DigestNotificationCronJob failed during tick processing.");
                    }

                    _logger.LogInformation("DigestNotificationCronJob tick complete.");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DigestNotificationCronJob stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DigestNotificationCronJob crashed.");
            }
        }
    }

    private async Task ProcessDigestRules(CancellationToken ct)
    {
        var rules = (await _queries.GetAllEnabledRulesByType("scheduled_digest")).ToList();
        if (rules.Count == 0) return;

        var isMonday = DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday;

        foreach (var rule in rules)
        {
            try
            {
                var config = JsonDocument.Parse(rule.ConfigJson).RootElement;
                var schedule = config.TryGetProperty("schedule", out var schedProp) ? schedProp.GetString() ?? "daily" : "daily";

                // Weekly digests only on Mondays
                if (schedule == "weekly" && !isMonday)
                    continue;

                var dateFrom = schedule == "weekly"
                    ? DateTime.UtcNow.Date.AddDays(-7)
                    : DateTime.UtcNow.Date.AddDays(-1);
                var dateTo = DateTime.UtcNow.Date;

                var digest = await _queryClient.NamedQuerySingleAsync<DigestResult>(
                    "notification_digest__v1",
                    new { app_id = rule.AppId, date_from = dateFrom, date_to = dateTo },
                    ct);

                var periodLabel = schedule == "weekly" ? "Weekly" : "Daily";
                var message = new StringBuilder();
                message.AppendLine($"Events: {digest.Events:N0}");
                message.AppendLine($"Sessions: {digest.Sessions:N0}");
                message.AppendLine($"Unique Users: {digest.Users:N0}");

                var dedupKey = $"digest:{rule.Id}:{dateTo:yyyy-MM-dd}";
                await _dispatcher.DispatchAsync(
                    rule,
                    $"{periodLabel} Digest",
                    message.ToString(),
                    dedupKey,
                    1440,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scheduled_digest rule {RuleId}", rule.Id);
            }
        }
    }
}
