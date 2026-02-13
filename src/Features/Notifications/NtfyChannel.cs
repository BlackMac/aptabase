using System.Net.Http.Headers;
using System.Text;

namespace Aptabase.Features.Notifications;

public class NtfyChannel : INotificationChannel
{
    private readonly string _serverUrl;
    private readonly string _topic;
    private readonly string? _token;
    private readonly HttpClient _http;

    public NtfyChannel(string serverUrl, string topic, string? token, HttpClient http)
    {
        _serverUrl = serverUrl?.TrimEnd('/') ?? "https://ntfy.sh";
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _token = token;
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task SendAsync(string title, string message, CancellationToken ct)
    {
        var url = $"{_serverUrl}/{_topic}";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(message, Encoding.UTF8, "text/plain")
        };

        request.Headers.Add("Title", title);
        request.Headers.Add("Tags", "bell");

        if (!string.IsNullOrEmpty(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
