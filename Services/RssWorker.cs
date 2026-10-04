using System.Globalization;
using System.Xml.Linq;

namespace News2Video.Services;

public class RssWorker
{
    private readonly HttpClient _httpClient;

    public RssWorker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<(string Title, string Url, DateTime PublishedAt)>> GetLatestAsync(
        string rssUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(rssUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException("Некорректный RSS URL.", nameof(rssUrl));

        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = XDocument.Parse(xml);

        var result = new List<(string Title, string Url, DateTime PublishedAt)>();

        foreach (var item in document.Descendants("item"))
        {
            var title = item.Element("title")?.Value.Trim() ?? string.Empty;
            var url = item.Element("link")?.Value.Trim() ?? string.Empty;
            var dateText = item.Element("pubDate")?.Value.Trim();

            if (string.IsNullOrWhiteSpace(url))
                continue;

            var publishedAt = DateTime.MinValue;

            if (!string.IsNullOrWhiteSpace(dateText) &&
                DateTimeOffset.TryParse(
                    dateText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsedDate))
            {
                publishedAt = parsedDate.LocalDateTime;
            }

            result.Add((title, url, publishedAt));
        }

        return result;
    }
}
