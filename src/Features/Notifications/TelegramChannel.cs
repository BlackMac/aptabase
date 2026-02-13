using System.Text;
using System.Text.Json;

namespace Aptabase.Features.Notifications;

public class TelegramChannel : INotificationChannel
{
    private readonly string _botToken;
    private readonly string _chatId;
    private readonly HttpClient _http;

    public TelegramChannel(string botToken, string chatId, HttpClient http)
    {
        _botToken = botToken ?? throw new ArgumentNullException(nameof(botToken));
        _chatId = chatId ?? throw new ArgumentNullException(nameof(chatId));
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task SendAsync(string title, string message, CancellationToken ct)
    {
        var text = $"*{EscapeMarkdown(title)}*\n\n{EscapeMarkdown(message)}";
        var payload = JsonSerializer.Serialize(new
        {
            chat_id = _chatId,
            text,
            parse_mode = "MarkdownV2"
        });

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content, ct);
        response.EnsureSuccessStatusCode();
    }

    private static string EscapeMarkdown(string text)
    {
        var chars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (Array.IndexOf(chars, c) >= 0)
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
