using System.Net.Http.Headers;
using News2Video.Models;

namespace News2Video.Services;

public class WebWorker
{
    private readonly HttpClient _httpClient;

    public WebWorker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HtmlDocument> GetHtmlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Некорректный URL.", nameof(url));

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("News2Video", "1.0"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return new HtmlDocument
        {
            Url = url,
            Content = content,
            ReceivedAt = DateTime.UtcNow
        };
    }
}
