using AngleSharp;
using News2Video.Models;

namespace News2Video.Services;

public class Parser
{
    public async Task<NewsContent> ParseAsync(
        HtmlDocument htmlDocument,
        CancellationToken cancellationToken = default)
    {
        var context = BrowsingContext.New(Configuration.Default);

        var document = await context.OpenAsync(
            request => request.Content(htmlDocument.Content),
            cancellationToken);

        var title =
            document.QuerySelector("h1")?.TextContent.Trim()
            ?? document.QuerySelector("title")?.TextContent.Trim()
            ?? "Без заголовка";

        var article =
            document.QuerySelector("article")
            ?? document.QuerySelector(".article")
            ?? document.QuerySelector("main");

        var text = article?.TextContent.Trim()
                   ?? document.Body?.TextContent.Trim()
                   ?? string.Empty;

        text = string.Join(
            Environment.NewLine,
            text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new NewsContent
        {
            Title = title,
            Text = text,
            Url = htmlDocument.Url,
            PublishedAt = htmlDocument.ReceivedAt
        };
    }
}