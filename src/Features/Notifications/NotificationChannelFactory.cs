using System.Text.Json;

namespace Aptabase.Features.Notifications;

public class NotificationChannelFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public NotificationChannelFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public INotificationChannel Create(NotificationChannelRow row)
    {
        var http = _httpClientFactory.CreateClient("Notifications");

        return row.ChannelType switch
        {
            "telegram" => CreateTelegram(row.ConfigJson, http),
            "pushover" => CreatePushover(row.ConfigJson, http),
            _ => throw new ArgumentException($"Unknown channel type: {row.ChannelType}")
        };
    }

    private static TelegramChannel CreateTelegram(string configJson, HttpClient http)
    {
        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;
        var botToken = root.GetProperty("bot_token").GetString() ?? "";
        var chatId = root.GetProperty("chat_id").GetString() ?? "";
        return new TelegramChannel(botToken, chatId, http);
    }

    private static PushoverChannel CreatePushover(string configJson, HttpClient http)
    {
        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;
        var userKey = root.GetProperty("user_key").GetString() ?? "";
        var appToken = root.GetProperty("app_token").GetString() ?? "";
        return new PushoverChannel(userKey, appToken, http);
    }
}
