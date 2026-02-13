using System.ComponentModel.DataAnnotations;

namespace Aptabase.Features.Notifications;

// Database row models (match table columns, Dapper maps with MatchNamesWithUnderscores)
public class NotificationChannelRow
{
    public string Id { get; set; } = "";
    public string AppId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ChannelType { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
}

public class NotificationRuleRow
{
    public string Id { get; set; } = "";
    public string AppId { get; set; } = "";
    public string RuleType { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
}

public class NotificationLogRow
{
    public string Id { get; set; } = "";
    public string AppId { get; set; } = "";
    public string? RuleId { get; set; }
    public string ChannelId { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
    public string? DedupKey { get; set; }
}

public class NotificationKnownValueRow
{
    public string AppId { get; set; } = "";
    public string ValueType { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset FirstSeenAt { get; set; }
}

// Rule with associated channel IDs (for API responses)
public class NotificationRuleWithChannels
{
    public string Id { get; set; } = "";
    public string AppId { get; set; } = "";
    public string RuleType { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public string[] ChannelIds { get; set; } = Array.Empty<string>();
}

// API request bodies
public class CreateChannelBody
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string ChannelType { get; set; } = "";

    [Required]
    public string ConfigJson { get; set; } = "{}";
}

public class UpdateChannelBody
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "";

    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
}

public class CreateRuleBody
{
    [Required]
    [StringLength(30)]
    public string RuleType { get; set; } = "";

    [Required]
    public string ConfigJson { get; set; } = "{}";

    public string[] ChannelIds { get; set; } = Array.Empty<string>();
}

public class UpdateRuleBody
{
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    public string[] ChannelIds { get; set; } = Array.Empty<string>();
}

// API response DTOs
public class NotificationChannelResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ChannelType { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class NotificationRuleResponse
{
    public string Id { get; set; } = "";
    public string RuleType { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; }
    public string[] ChannelIds { get; set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; set; }
}

public class NotificationLogResponse
{
    public string Id { get; set; } = "";
    public string? RuleId { get; set; }
    public string ChannelId { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public string? RuleType { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}

// ClickHouse query results
public class EventCountResult
{
    public string AppId { get; set; } = "";
    public string EventName { get; set; } = "";
    public long Count { get; set; }
}

public class DistinctValueResult
{
    public string AppId { get; set; } = "";
    public string Value { get; set; } = "";
}

public class DailyVolumeResult
{
    public string AppId { get; set; } = "";
    public DateTime Date { get; set; }
    public long Count { get; set; }
}

public class LastEventResult
{
    public string AppId { get; set; } = "";
    public DateTime LastTimestamp { get; set; }
}

public class DigestResult
{
    public long Events { get; set; }
    public long Sessions { get; set; }
    public long Users { get; set; }
}

public class TopEventResult
{
    public string Name { get; set; } = "";
    public long Value { get; set; }
}
