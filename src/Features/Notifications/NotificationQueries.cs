using Aptabase.Data;
using Aptabase.Features.Stats;
using Dapper;

namespace Aptabase.Features.Notifications;

public interface INotificationQueries
{
    Task<IEnumerable<NotificationChannelRow>> GetChannelsForApp(string appId);
    Task<NotificationChannelRow?> GetChannelById(string appId, string channelId);
    Task<NotificationChannelRow> CreateChannel(string appId, string name, string channelType, string configJson);
    Task UpdateChannel(string channelId, string name, string configJson, bool enabled);
    Task DeleteChannel(string channelId);

    Task<IEnumerable<NotificationRuleWithChannels>> GetRulesForApp(string appId);
    Task<NotificationRuleWithChannels?> GetRuleById(string appId, string ruleId);
    Task<NotificationRuleWithChannels> CreateRule(string appId, string ruleType, string configJson, string[] channelIds);
    Task UpdateRule(string ruleId, string configJson, bool enabled, string[] channelIds);
    Task DeleteRule(string ruleId);

    Task<IEnumerable<NotificationRuleWithChannels>> GetAllEnabledRulesByType(params string[] ruleTypes);
    Task<IEnumerable<NotificationKnownValueRow>> GetKnownValues(string appId, string valueType);
    Task UpsertKnownValues(string appId, string valueType, IEnumerable<string> values);
    Task<bool> HasRecentNotification(string dedupKey, int windowMinutes);
    Task LogNotification(string appId, string? ruleId, string channelId, string message, string? dedupKey);
    Task<IEnumerable<NotificationLogResponse>> GetRecentLogs(string appId, int limit = 50);
    Task<int> GetRecentNotificationCount(string appId, int windowMinutes);
    Task<IEnumerable<string>> GetEventNamesForApp(string appId, CancellationToken ct);
}

public class NotificationQueries : INotificationQueries
{
    private readonly IDbContext _db;
    private readonly IQueryClient _queryClient;

    public NotificationQueries(IDbContext db, IQueryClient queryClient)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
    }

    public async Task<IEnumerable<NotificationChannelRow>> GetChannelsForApp(string appId)
    {
        return await _db.Connection.QueryAsync<NotificationChannelRow>(
            "SELECT * FROM notification_channels WHERE app_id = @appId ORDER BY created_at",
            new { appId });
    }

    public async Task<NotificationChannelRow?> GetChannelById(string appId, string channelId)
    {
        return await _db.Connection.QueryFirstOrDefaultAsync<NotificationChannelRow>(
            "SELECT * FROM notification_channels WHERE id = @channelId AND app_id = @appId",
            new { channelId, appId });
    }

    public async Task<NotificationChannelRow> CreateChannel(string appId, string name, string channelType, string configJson)
    {
        var id = NanoId.New();
        await _db.Connection.ExecuteAsync(@"
            INSERT INTO notification_channels (id, app_id, name, channel_type, config_json, enabled)
            VALUES (@id, @appId, @name, @channelType, @configJson, true)",
            new { id, appId, name, channelType, configJson });

        return (await _db.Connection.QueryFirstAsync<NotificationChannelRow>(
            "SELECT * FROM notification_channels WHERE id = @id", new { id }));
    }

    public async Task UpdateChannel(string channelId, string name, string configJson, bool enabled)
    {
        await _db.Connection.ExecuteAsync(@"
            UPDATE notification_channels
            SET name = @name, config_json = @configJson, enabled = @enabled, modified_at = now()
            WHERE id = @channelId",
            new { channelId, name, configJson, enabled });
    }

    public async Task DeleteChannel(string channelId)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM notification_rule_channels WHERE channel_id = @channelId", new { channelId });
        await _db.Connection.ExecuteAsync("DELETE FROM notification_channels WHERE id = @channelId", new { channelId });
    }

    public async Task<IEnumerable<NotificationRuleWithChannels>> GetRulesForApp(string appId)
    {
        var rules = await _db.Connection.QueryAsync<NotificationRuleRow>(
            "SELECT * FROM notification_rules WHERE app_id = @appId ORDER BY created_at",
            new { appId });

        var ruleIds = rules.Select(r => r.Id).ToArray();
        if (ruleIds.Length == 0)
            return Enumerable.Empty<NotificationRuleWithChannels>();

        var joins = await _db.Connection.QueryAsync<(string RuleId, string ChannelId)>(
            "SELECT rule_id, channel_id FROM notification_rule_channels WHERE rule_id = ANY(@ruleIds)",
            new { ruleIds });

        var channelsByRule = joins.GroupBy(j => j.RuleId)
            .ToDictionary(g => g.Key, g => g.Select(j => j.ChannelId).ToArray());

        return rules.Select(r => new NotificationRuleWithChannels
        {
            Id = r.Id,
            AppId = r.AppId,
            RuleType = r.RuleType,
            ConfigJson = r.ConfigJson,
            Enabled = r.Enabled,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
            ChannelIds = channelsByRule.GetValueOrDefault(r.Id, Array.Empty<string>())
        });
    }

    public async Task<NotificationRuleWithChannels?> GetRuleById(string appId, string ruleId)
    {
        var rule = await _db.Connection.QueryFirstOrDefaultAsync<NotificationRuleRow>(
            "SELECT * FROM notification_rules WHERE id = @ruleId AND app_id = @appId",
            new { ruleId, appId });

        if (rule == null) return null;

        var channelIds = (await _db.Connection.QueryAsync<string>(
            "SELECT channel_id FROM notification_rule_channels WHERE rule_id = @ruleId",
            new { ruleId })).ToArray();

        return new NotificationRuleWithChannels
        {
            Id = rule.Id,
            AppId = rule.AppId,
            RuleType = rule.RuleType,
            ConfigJson = rule.ConfigJson,
            Enabled = rule.Enabled,
            CreatedAt = rule.CreatedAt,
            ModifiedAt = rule.ModifiedAt,
            ChannelIds = channelIds
        };
    }

    public async Task<NotificationRuleWithChannels> CreateRule(string appId, string ruleType, string configJson, string[] channelIds)
    {
        var id = NanoId.New();
        await _db.Connection.ExecuteAsync(@"
            INSERT INTO notification_rules (id, app_id, rule_type, config_json, enabled)
            VALUES (@id, @appId, @ruleType, @configJson, true)",
            new { id, appId, ruleType, configJson });

        foreach (var channelId in channelIds)
        {
            await _db.Connection.ExecuteAsync(@"
                INSERT INTO notification_rule_channels (rule_id, channel_id)
                VALUES (@ruleId, @channelId)
                ON CONFLICT DO NOTHING",
                new { ruleId = id, channelId });
        }

        return (await GetRuleById(appId, id))!;
    }

    public async Task UpdateRule(string ruleId, string configJson, bool enabled, string[] channelIds)
    {
        await _db.Connection.ExecuteAsync(@"
            UPDATE notification_rules
            SET config_json = @configJson, enabled = @enabled, modified_at = now()
            WHERE id = @ruleId",
            new { ruleId, configJson, enabled });

        await _db.Connection.ExecuteAsync(
            "DELETE FROM notification_rule_channels WHERE rule_id = @ruleId",
            new { ruleId });

        foreach (var channelId in channelIds)
        {
            await _db.Connection.ExecuteAsync(@"
                INSERT INTO notification_rule_channels (rule_id, channel_id)
                VALUES (@ruleId, @channelId)
                ON CONFLICT DO NOTHING",
                new { ruleId, channelId });
        }
    }

    public async Task DeleteRule(string ruleId)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM notification_rule_channels WHERE rule_id = @ruleId", new { ruleId });
        await _db.Connection.ExecuteAsync("DELETE FROM notification_rules WHERE id = @ruleId", new { ruleId });
    }

    public async Task<IEnumerable<NotificationRuleWithChannels>> GetAllEnabledRulesByType(params string[] ruleTypes)
    {
        var rules = await _db.Connection.QueryAsync<NotificationRuleRow>(
            "SELECT * FROM notification_rules WHERE enabled = true AND rule_type = ANY(@ruleTypes)",
            new { ruleTypes });

        var ruleIds = rules.Select(r => r.Id).ToArray();
        if (ruleIds.Length == 0)
            return Enumerable.Empty<NotificationRuleWithChannels>();

        var joins = await _db.Connection.QueryAsync<(string RuleId, string ChannelId)>(
            "SELECT rule_id, channel_id FROM notification_rule_channels WHERE rule_id = ANY(@ruleIds)",
            new { ruleIds });

        var channelsByRule = joins.GroupBy(j => j.RuleId)
            .ToDictionary(g => g.Key, g => g.Select(j => j.ChannelId).ToArray());

        return rules.Select(r => new NotificationRuleWithChannels
        {
            Id = r.Id,
            AppId = r.AppId,
            RuleType = r.RuleType,
            ConfigJson = r.ConfigJson,
            Enabled = r.Enabled,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
            ChannelIds = channelsByRule.GetValueOrDefault(r.Id, Array.Empty<string>())
        });
    }

    public async Task<IEnumerable<NotificationKnownValueRow>> GetKnownValues(string appId, string valueType)
    {
        return await _db.Connection.QueryAsync<NotificationKnownValueRow>(
            "SELECT * FROM notification_known_values WHERE app_id = @appId AND value_type = @valueType",
            new { appId, valueType });
    }

    public async Task UpsertKnownValues(string appId, string valueType, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            await _db.Connection.ExecuteAsync(@"
                INSERT INTO notification_known_values (app_id, value_type, value, first_seen_at)
                VALUES (@appId, @valueType, @value, now())
                ON CONFLICT (app_id, value_type, value) DO NOTHING",
                new { appId, valueType, value });
        }
    }

    public async Task<bool> HasRecentNotification(string dedupKey, int windowMinutes)
    {
        var count = await _db.Connection.QueryFirstAsync<int>(@"
            SELECT COUNT(*) FROM notification_log
            WHERE dedup_key = @dedupKey
            AND sent_at > now() - make_interval(mins => @windowMinutes)",
            new { dedupKey, windowMinutes });
        return count > 0;
    }

    public async Task LogNotification(string appId, string? ruleId, string channelId, string message, string? dedupKey)
    {
        var id = NanoId.New();
        await _db.Connection.ExecuteAsync(@"
            INSERT INTO notification_log (id, app_id, rule_id, channel_id, message, sent_at, dedup_key)
            VALUES (@id, @appId, @ruleId, @channelId, @message, now(), @dedupKey)",
            new { id, appId, ruleId, channelId, message, dedupKey });
    }

    public async Task<IEnumerable<NotificationLogResponse>> GetRecentLogs(string appId, int limit = 50)
    {
        return await _db.Connection.QueryAsync<NotificationLogResponse>(@"
            SELECT l.id, l.rule_id, l.channel_id, c.name as channel_name, r.rule_type, l.message, l.sent_at
            FROM notification_log l
            LEFT JOIN notification_channels c ON c.id = l.channel_id
            LEFT JOIN notification_rules r ON r.id = l.rule_id
            WHERE l.app_id = @appId
            ORDER BY l.sent_at DESC
            LIMIT @limit",
            new { appId, limit });
    }

    public async Task<int> GetRecentNotificationCount(string appId, int windowMinutes)
    {
        return await _db.Connection.QueryFirstAsync<int>(@"
            SELECT COUNT(*) FROM notification_log
            WHERE app_id = @appId
            AND sent_at > now() - make_interval(mins => @windowMinutes)",
            new { appId, windowMinutes });
    }

    public async Task<IEnumerable<string>> GetEventNamesForApp(string appId, CancellationToken ct)
    {
        var results = await _queryClient.NamedQueryAsync<DistinctValueResult>(
            "notification_distinct_values__v1",
            new { app_ids = new[] { appId }, column_name = "event_name", since = DateTime.UtcNow.AddDays(-30) },
            ct);
        return results.Select(r => r.Value);
    }
}
