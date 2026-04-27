using Aptabase.Data;
using Aptabase.Features.GeoIP;
using Aptabase.Features.Stats;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aptabase.Features.ReadApi;

public record ReadApiApp
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AppKey { get; set; } = "";
    public bool HasEvents { get; set; }
}

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting("Stats")]
public class ReadApiController : Controller
{
    private readonly IQueryClient _queryClient;
    private readonly IDbContext _db;
    private readonly GeoIPClient _geodb;
    private readonly EnvSettings _env;

    public ReadApiController(IQueryClient queryClient, IDbContext db, GeoIPClient geodb, EnvSettings env)
    {
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _geodb = geodb ?? throw new ArgumentNullException(nameof(geodb));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    [HttpGet("/api/read/apps"), HasValidReadApiToken]
    public async Task<IActionResult> ListApps(CancellationToken cancellationToken)
    {
        var rows = await _db.Connection.QueryAsync<ReadApiApp>(new CommandDefinition(
            @"SELECT a.id, a.name, a.app_key, a.has_events
              FROM apps a
              INNER JOIN users u ON u.id = a.owner_id
              WHERE a.deleted_at IS NULL
                AND u.lock_reason IS NULL
              ORDER BY a.name",
            cancellationToken: cancellationToken));
        return Ok(rows);
    }

    [HttpGet("/api/read/key-metrics"), HasValidReadApiToken]
    public async Task<IActionResult> KeyMetrics([FromQuery] QueryParams body, CancellationToken cancellationToken)
    {
        var current = body.Parse();
        var currentRow = await _queryClient.NamedQuerySingleAsync<KeyMetricsRow>("key_metrics__v2", QueryArgsToParams(current), cancellationToken);

        if (!current.DateFrom.HasValue || !current.DateTo.HasValue)
            return Ok(new KeyMetrics(currentRow));

        var previous = current.CloneToPreviousInterval();
        var previousRow = await _queryClient.NamedQuerySingleAsync<KeyMetricsRow>("key_metrics__v2", QueryArgsToParams(previous), cancellationToken);
        return Ok(new KeyMetrics(currentRow, previousRow));
    }

    [HttpGet("/api/read/periodic"), HasValidReadApiToken]
    public async Task<IActionResult> PeriodicStats([FromQuery] QueryParams body, CancellationToken cancellationToken)
    {
        var query = body.Parse();
        var rows = await _queryClient.NamedQueryAsync<PeriodicStatsRow>("key_metrics_periodic__v2", QueryArgsToParams(query), cancellationToken);
        return Ok(rows);
    }

    [HttpGet("/api/read/top-countries"), HasValidReadApiToken]
    public Task<IActionResult> TopCountries([FromQuery] QueryParams body, CancellationToken cancellationToken)
        => TopN("country_code", body.Granularity ?? GranularityEnum.Hour, TopNValue.UniqueSessions, body, cancellationToken);

    [HttpGet("/api/read/top-regions"), HasValidReadApiToken]
    public Task<IActionResult> TopRegions([FromQuery] QueryParams body, CancellationToken cancellationToken)
        => TopN("region_name", body.Granularity ?? GranularityEnum.Hour, TopNValue.UniqueSessions, body, cancellationToken);

    [HttpGet("/api/read/top-osversions"), HasValidReadApiToken]
    public Task<IActionResult> TopOSVersions([FromQuery] QueryParams body, CancellationToken cancellationToken)
        => TopN("os_version", body.Granularity ?? GranularityEnum.Hour, TopNValue.UniqueSessions, body, cancellationToken);

    [HttpGet("/api/read/top-operatingsystems"), HasValidReadApiToken]
    public Task<IActionResult> TopOperatingSystems([FromQuery] QueryParams body, CancellationToken cancellationToken)
        => TopN("os_name", body.Granularity ?? GranularityEnum.Hour, TopNValue.UniqueSessions, body, cancellationToken);

    [HttpGet("/api/read/top-devices"), HasValidReadApiToken]
    public Task<IActionResult> TopDevices([FromQuery] QueryParams body, CancellationToken cancellationToken)
        => TopN("device_model", body.Granularity ?? GranularityEnum.Hour, TopNValue.UniqueSessions, body, cancellationToken);

    [HttpGet("/api/read/top-events"), HasValidReadApiToken]
    public Task<IActionResult> TopEvents([FromQuery] QueryParams body, CancellationToken cancellationToken)
        => TopN("event_name", body.Granularity ?? GranularityEnum.Hour, TopNValue.TotalEvents, body, cancellationToken);

    [HttpGet("/api/read/top-appversions"), HasValidReadApiToken]
    public Task<IActionResult> TopAppVersions([FromQuery] QueryParams body, CancellationToken cancellationToken)
        => TopN("app_version", body.Granularity ?? GranularityEnum.Hour, TopNValue.UniqueSessions, body, cancellationToken);

    [HttpGet("/api/read/top-appbuildnumbers"), HasValidReadApiToken]
    public Task<IActionResult> TopAppBuildNumbers([FromQuery] QueryParams body, CancellationToken cancellationToken)
        => TopN("app_build_number", body.Granularity ?? GranularityEnum.Hour, TopNValue.UniqueSessions, body, cancellationToken);

    [HttpGet("/api/read/top-props"), HasValidReadApiToken]
    public async Task<IActionResult> EventProps([FromQuery] QueryParams body, CancellationToken cancellationToken)
    {
        var query = body.Parse();
        var rows = await _queryClient.NamedQueryAsync<EventPropsItem>("top_props__v2", QueryArgsToParams(query), cancellationToken);
        return Ok(rows);
    }

    [HttpGet("/api/read/live-geo"), HasValidReadApiToken]
    public async Task<IActionResult> LiveGeo([FromQuery] QueryParams body, CancellationToken cancellationToken)
    {
        var query = body.Parse();
        var rows = await _queryClient.NamedQueryAsync<LiveGeoDataPoint>("live_geo__v1", new { app_id = query.AppId }, cancellationToken);
        foreach (var row in rows)
        {
            var (lat, lng) = _geodb.GetLatLng(row.CountryCode, row.RegionName);
            row.Latitude = lat;
            row.Longitude = lng;
        }
        return Ok(rows);
    }

    [HttpGet("/api/read/live-sessions"), HasValidReadApiToken]
    public async Task<IActionResult> LiveSessions([FromQuery] QueryParams body, CancellationToken cancellationToken)
    {
        var query = body.Parse();
        var rows = await _queryClient.NamedQueryAsync<LiveRecentSession>("live_sessions__v1", new { app_id = query.AppId }, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("/api/read/live-session-details"), HasValidReadApiToken]
    public async Task<IActionResult> LiveSessionDetails([FromQuery] QueryParams body, CancellationToken cancellationToken)
    {
        var query = body.Parse();
        var row = await _queryClient.NamedQuerySingleAsync<SessionTimeline>("live_session_details__v1", new
        {
            app_id = query.AppId,
            session_id = query.SessionId,
        }, cancellationToken);
        return Ok(row);
    }

    [HttpGet("/api/read/historical-sessions"), HasValidReadApiToken]
    public async Task<IActionResult> HistoricalSessions([FromQuery] QueryParams body, CancellationToken cancellationToken)
    {
        var query = body.Parse();
        var rows = await _queryClient.NamedQueryAsync<LiveRecentSession>("historical_sessions__v1", new
        {
            app_id = query.AppId,
            session_id = query.SessionId,
            date_from = query.DateFrom?.ToString("yyyy-MM-dd HH:mm:ss"),
            date_to = query.DateTo?.ToString("yyyy-MM-dd HH:mm:ss"),
            event_name = query.EventName,
            os_name = query.OsName,
            app_version = query.AppVersion,
            country_code = query.CountryCode
        }, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("/api/llms.txt")]
    [Produces("text/plain")]
    public IActionResult LlmsTxt()
    {
        var baseUrl = _env.SelfBaseUrl.TrimEnd('/');
        var enabled = string.IsNullOrEmpty(_env.ReadApiToken) ? "disabled" : "enabled";
        var doc = $@"# Aptabase Reading API

Aptabase exposes a read-only HTTP API for analytics data at {baseUrl}/api/read/*.
Status on this instance: {enabled} (controlled by the READ_API_TOKEN environment variable).

## Authentication

All endpoints under /api/read/* require a static bearer token:

    Authorization: Bearer <READ_API_TOKEN>

The token is configured by setting the READ_API_TOKEN environment variable on the
Aptabase server. When the variable is unset or empty, every /api/read/* endpoint
returns HTTP 503. A wrong or missing token returns HTTP 401.

The token grants read access to every app on the instance. Treat it as a server
secret; do not embed it in client applications.

## Common query parameters

Most stats endpoints accept these query string parameters:

  AppId         (required) - the internal app id (see /api/read/apps)
  BuildMode     'release' (default) or 'debug' to read events from the _DEBUG bucket
  StartDate     ISO-8601 UTC timestamp, inclusive lower bound
  EndDate       ISO-8601 UTC timestamp, inclusive upper bound
  Granularity   'Hour' (default), 'Day', or 'Month' for time-bucketed responses
  CountryCode   ISO 3166-1 alpha-2 filter
  OsName        OS name filter (e.g. 'iOS', 'Android', 'Windows')
  DeviceModel   Device model filter
  EventName     Event name filter
  AppVersion    App version filter

SessionId is required for /api/read/live-session-details and optional for
/api/read/historical-sessions.

## Endpoints

  GET /api/read/apps
      List all active apps on the instance. Returns id, name, app_key, has_events.

  GET /api/read/key-metrics
      Aggregate metrics for the period (daily users, sessions, events,
      duration). When StartDate and EndDate are provided, the previous
      equally-sized interval is included for comparison.

  GET /api/read/periodic
      Time-bucketed users/sessions/events series at the requested granularity.

  GET /api/read/top-countries
  GET /api/read/top-regions
  GET /api/read/top-osversions
  GET /api/read/top-operatingsystems
  GET /api/read/top-devices
  GET /api/read/top-events
  GET /api/read/top-appversions
  GET /api/read/top-appbuildnumbers
      Top-N breakdown for the dimension. top-events ranks by total events;
      every other endpoint ranks by unique sessions.

  GET /api/read/top-props
      Top values for custom string and numeric event properties.

  GET /api/read/live-geo
      Active users in the last few minutes grouped by country/region with
      lat/lng coordinates.

  GET /api/read/live-sessions
      Recent live sessions (no date filters).

  GET /api/read/live-session-details?AppId=...&SessionId=...
      Full event timeline for one live session.

  GET /api/read/historical-sessions
      Past sessions matching the filters.

  GET /api/llms.txt
      This document. Public, no token required.

## Example

    curl -H ""Authorization: Bearer $READ_API_TOKEN"" \
         ""{baseUrl}/api/read/key-metrics?AppId=YOUR_APP_ID&StartDate=2026-04-01T00:00:00Z&EndDate=2026-04-27T23:59:59Z""

## Rate limiting

Reading endpoints share the 'Stats' rate limit policy: 1000 requests per hour
per client IP.
";
        return Content(doc, "text/plain; charset=utf-8");
    }

    private async Task<IActionResult> TopN(string nameColumn, GranularityEnum granularity, TopNValue valueColumn, QueryParams body, CancellationToken cancellationToken)
    {
        var query = body.Parse();
        var rows = await _queryClient.NamedQueryAsync<TopNItem>("top_n__v2", new
        {
            name_column = nameColumn,
            granularity = granularity.ToString(),
            value_column = valueColumn.ToString(),
            date_from = query.DateFrom?.ToString("yyyy-MM-dd HH:mm:ss"),
            date_to = query.DateTo?.ToString("yyyy-MM-dd HH:mm:ss"),
            app_id = query.AppId,
            event_name = query.EventName,
            os_name = query.OsName,
            app_version = query.AppVersion,
            country_code = query.CountryCode,
            device_model = query.DeviceModel,
        }, cancellationToken);
        return Ok(rows);
    }

    private static object QueryArgsToParams(QueryArgs query) => new
    {
        date_from = query.DateFrom?.ToString("yyyy-MM-dd HH:mm:ss"),
        date_to = query.DateTo?.ToString("yyyy-MM-dd HH:mm:ss"),
        granularity = (query.Granularity ?? GranularityEnum.Hour).ToString(),
        app_id = query.AppId,
        event_name = query.EventName,
        os_name = query.OsName,
        app_version = query.AppVersion,
        country_code = query.CountryCode,
        device_model = query.DeviceModel,
    };
}
