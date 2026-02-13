using Aptabase.Data;
using Aptabase.Features.Authentication;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Aptabase.Features.Notifications;

[ApiController, IsAuthenticated]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController : Controller
{
    private readonly IDbContext _db;
    private readonly INotificationQueries _queries;
    private readonly NotificationDispatcher _dispatcher;

    public NotificationsController(IDbContext db, INotificationQueries queries, NotificationDispatcher dispatcher)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    // --- Channels ---

    [HttpGet("/api/_apps/{appId}/notification-channels")]
    public async Task<IActionResult> ListChannels(string appId)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var channels = await _queries.GetChannelsForApp(appId);
        var response = channels.Select(c => new NotificationChannelResponse
        {
            Id = c.Id,
            Name = c.Name,
            ChannelType = c.ChannelType,
            ConfigJson = c.ConfigJson,
            Enabled = c.Enabled,
            CreatedAt = c.CreatedAt,
        });
        return Ok(response);
    }

    [HttpPost("/api/_apps/{appId}/notification-channels")]
    public async Task<IActionResult> CreateChannel(string appId, [FromBody] CreateChannelBody body)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var validTypes = new[] { "telegram", "pushover", "ntfy" };
        if (!validTypes.Contains(body.ChannelType))
            return BadRequest(new { errors = new { channelType = new[] { "Channel type must be 'telegram', 'pushover', or 'ntfy'" } } });

        var channel = await _queries.CreateChannel(appId, body.Name, body.ChannelType, body.ConfigJson);
        return Ok(new NotificationChannelResponse
        {
            Id = channel.Id,
            Name = channel.Name,
            ChannelType = channel.ChannelType,
            ConfigJson = channel.ConfigJson,
            Enabled = channel.Enabled,
            CreatedAt = channel.CreatedAt,
        });
    }

    [HttpPut("/api/_apps/{appId}/notification-channels/{channelId}")]
    public async Task<IActionResult> UpdateChannel(string appId, string channelId, [FromBody] UpdateChannelBody body)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var existing = await _queries.GetChannelById(appId, channelId);
        if (existing == null) return NotFound();

        // If configJson contains only empty/default values, keep existing config
        var configJson = body.ConfigJson == "{}" ? existing.ConfigJson : body.ConfigJson;

        await _queries.UpdateChannel(channelId, body.Name, configJson, body.Enabled);
        return Ok(new NotificationChannelResponse
        {
            Id = existing.Id,
            Name = body.Name,
            ChannelType = existing.ChannelType,
            ConfigJson = configJson,
            Enabled = body.Enabled,
            CreatedAt = existing.CreatedAt,
        });
    }

    [HttpDelete("/api/_apps/{appId}/notification-channels/{channelId}")]
    public async Task<IActionResult> DeleteChannel(string appId, string channelId)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var existing = await _queries.GetChannelById(appId, channelId);
        if (existing == null) return NotFound();

        await _queries.DeleteChannel(channelId);
        return Ok(new { });
    }

    [HttpPost("/api/_apps/{appId}/notification-channels/{channelId}/test")]
    public async Task<IActionResult> TestChannel(string appId, string channelId, CancellationToken ct)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var channel = await _queries.GetChannelById(appId, channelId);
        if (channel == null) return NotFound();

        await _dispatcher.SendTestAsync(appId, channel, ct);
        return Ok(new { });
    }

    // --- Rules ---

    [HttpGet("/api/_apps/{appId}/notification-rules")]
    public async Task<IActionResult> ListRules(string appId)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var rules = await _queries.GetRulesForApp(appId);
        var response = rules.Select(r => new NotificationRuleResponse
        {
            Id = r.Id,
            RuleType = r.RuleType,
            ConfigJson = r.ConfigJson,
            Enabled = r.Enabled,
            ChannelIds = r.ChannelIds,
            CreatedAt = r.CreatedAt,
        });
        return Ok(response);
    }

    [HttpPost("/api/_apps/{appId}/notification-rules")]
    public async Task<IActionResult> CreateRule(string appId, [FromBody] CreateRuleBody body)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var rule = await _queries.CreateRule(appId, body.RuleType, body.ConfigJson, body.ChannelIds);
        return Ok(new NotificationRuleResponse
        {
            Id = rule.Id,
            RuleType = rule.RuleType,
            ConfigJson = rule.ConfigJson,
            Enabled = rule.Enabled,
            ChannelIds = rule.ChannelIds,
            CreatedAt = rule.CreatedAt,
        });
    }

    [HttpPut("/api/_apps/{appId}/notification-rules/{ruleId}")]
    public async Task<IActionResult> UpdateRule(string appId, string ruleId, [FromBody] UpdateRuleBody body)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var existing = await _queries.GetRuleById(appId, ruleId);
        if (existing == null) return NotFound();

        await _queries.UpdateRule(ruleId, body.ConfigJson, body.Enabled, body.ChannelIds);
        return Ok(new NotificationRuleResponse
        {
            Id = existing.Id,
            RuleType = existing.RuleType,
            ConfigJson = body.ConfigJson,
            Enabled = body.Enabled,
            ChannelIds = body.ChannelIds,
            CreatedAt = existing.CreatedAt,
        });
    }

    [HttpDelete("/api/_apps/{appId}/notification-rules/{ruleId}")]
    public async Task<IActionResult> DeleteRule(string appId, string ruleId)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var existing = await _queries.GetRuleById(appId, ruleId);
        if (existing == null) return NotFound();

        await _queries.DeleteRule(ruleId);
        return Ok(new { });
    }

    // --- Log ---

    [HttpGet("/api/_apps/{appId}/notification-log")]
    public async Task<IActionResult> GetLog(string appId)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var logs = await _queries.GetRecentLogs(appId);
        return Ok(logs);
    }

    // --- Event Names ---

    [HttpGet("/api/_apps/{appId}/event-names")]
    public async Task<IActionResult> GetEventNames(string appId, CancellationToken ct)
    {
        if (await GetOwnedApp(appId) == null) return NotFound();

        var names = await _queries.GetEventNamesForApp(appId, ct);
        return Ok(names);
    }

    // --- Helpers ---

    private async Task<dynamic?> GetOwnedApp(string appId)
    {
        var user = this.GetCurrentUserIdentity();
        return await _db.Connection.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT a.id
              FROM apps a
              WHERE a.id = @appId
              AND a.owner_id = @userId
              AND a.deleted_at IS NULL",
            new { appId, userId = user.Id });
    }
}
