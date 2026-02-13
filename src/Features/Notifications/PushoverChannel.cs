namespace Aptabase.Features.Notifications;

public class PushoverChannel : INotificationChannel
{
    private readonly string _userKey;
    private readonly string _appToken;
    private readonly HttpClient _http;

    public PushoverChannel(string userKey, string appToken, HttpClient http)
    {
        _userKey = userKey ?? throw new ArgumentNullException(nameof(userKey));
        _appToken = appToken ?? throw new ArgumentNullException(nameof(appToken));
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task SendAsync(string title, string message, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", _appToken),
            new KeyValuePair<string, string>("user", _userKey),
            new KeyValuePair<string, string>("title", title),
            new KeyValuePair<string, string>("message", message),
        });

        var response = await _http.PostAsync("https://api.pushover.net/1/messages.json", form, ct);
        response.EnsureSuccessStatusCode();
    }
}
