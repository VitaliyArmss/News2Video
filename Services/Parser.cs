using AngleSharp;
using AngleSharp.Dom;
using News2Video.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace News2Video.Services;

public class Parser
{
    private const int MaxTextLength = 1400;

    public async Task<NewsContent> ParseAsync(
        HtmlDocument htmlDocument,
        CancellationToken cancellationToken = default)
    {
        var context = BrowsingContext.New(Configuration.Default);

        var document = await context.OpenAsync(
            request => request.Content(htmlDocument.Content),
            cancellationToken);

        var title = ExtractTitle(document);

        var article = FindArticle(document);

        if (article is null)
        {
            return new NewsContent
            {
                Title = title,
                Text = string.Empty,
                Url = htmlDocument.Url,
                PublishedAt = htmlDocument.ReceivedAt
            };
        }

        CleanupArticle(article);

        var text = ExtractArticleText(article);

        text = TrimToParagraph(text, MaxTextLength);

        return new NewsContent
        {
            Title = title,
            Text = text,
            Url = htmlDocument.Url,
            PublishedAt = htmlDocument.ReceivedAt
        };
    }

    private static string ExtractTitle(IDocument document)
    {
        return
            document.QuerySelector("h1[itemprop='headline']")
                ?.TextContent.Trim()

            ?? document.QuerySelector("h1")
                ?.TextContent.Trim()

            ?? document.QuerySelector("meta[property='og:title']")
                ?.GetAttribute("content")?.Trim()

            ?? document.QuerySelector("meta[name='twitter:title']")
                ?.GetAttribute("content")?.Trim()

            ?? document.QuerySelector("title")
                ?.TextContent.Trim()

            ?? "Без заголовка";
    }

    private static IElement? FindArticle(IDocument document)
    {
        var article = document.QuerySelector(
            "[itemprop='articleBody']");

        if (article is not null)
            return article;

        article = document.QuerySelector("article");

        if (article is not null)
            return article;

        article = document.QuerySelector(
            ".article-body, " +
            ".article-content, " +
            ".article-text, " +
            ".entry-body, " +
            ".entry-content, " +
            ".post-content, " +
            ".post-body, " +
            ".news-content");

        if (article is not null)
            return article;

        return document.QuerySelector("main");
    }

    private static void CleanupArticle(IElement article)
    {
        Remove(article,
            "script",
            "style",
            "noscript",
            "template",
            "svg",
            "canvas");

        Remove(article,
            "img",
            "picture",
            "source",
            "video",
            "audio",
            "iframe");

        Remove(article,
            ".ad",
            ".ads",
            ".advert",
            ".advertisement",
            ".advertising",
            ".banner",
            ".adsbygoogle",
            ".resp-ad-zone",
            ".df-player",
            "[id*='adfox']",
            "[id*='yandex_rtb']",
            "[id*='advert']",
            "[class*='advert']",
            "[class*='adsbygoogle']",
            "[class*='adfox']");

        Remove(article,
            ".slider-container",
            ".related",
            ".relatedbox",
            ".related-slider",
            ".recommendations",
            ".recommended",
            ".recommended-posts",
            ".related-posts",
            ".similar",
            ".similar-articles");

        Remove(article,
            ".share",
            ".social",
            ".social-buttons",
            ".sharing",
            ".share-buttons");

        Remove(article,
            ".comments",
            ".comment-list",
            ".comments-container",
            "#comments");

        Remove(article,
            ".promo",
            ".promotion",
            ".sponsor",
            ".sponsored",
            ".ps-promo");

        Remove(article,
            ".source-wrapper",
            ".caption-wrapper",
            ".caption",
            ".image-caption",
            ".photo-caption",
            "[class*='caption']");

        Remove(article,
            ".sources_rel",
            ".source",
            ".article-source",
            ".typo-hint");

        Remove(article,
            ".reddit-embed-bq",
            ".reddit-embed",
            ".twitter-tweet",
            ".instagram-media",
            ".fb-post",
            ".vk-post",
            ".telegram-post");

        foreach (var element in article.QuerySelectorAll(
                     "[style*='font-size: 0']"))
        {
            element.Remove();
        }
    }

    private static string ExtractArticleText(IElement article)
    {
        var blocks = article
            .QuerySelectorAll(
                "h2, h3, h4, h5, h6, p, li, blockquote, pre")
            .Select(x => NormalizeText(x.TextContent))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            blocks);
    }

    private static string TrimToParagraph(
        string text,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        var paragraphs = text
            .Split(
                new[] { "\r\n\r\n", "\n\n", "\r\r" },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (paragraphs.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        var separator = Environment.NewLine + Environment.NewLine;

        foreach (var paragraph in paragraphs)
        {
            var candidateLength = builder.Length == 0
                ? paragraph.Length
                : builder.Length + separator.Length + paragraph.Length;

            if (candidateLength > maxLength)
                break;

            if (builder.Length > 0)
                builder.Append(separator);

            builder.Append(paragraph);
        }

        if (builder.Length > 0)
            return builder.ToString();

        var first = paragraphs[0];

        if (first.Length <= maxLength)
            return first;

        var cut = first.LastIndexOf(' ', maxLength);

        if (cut <= 0)
            cut = maxLength;

        return first[..cut].TrimEnd() + "…";
    }

    private static void Remove(
        IElement root,
        params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var elements = root
                .QuerySelectorAll(selector)
                .ToList();

            foreach (var element in elements)
            {
                element.Remove();
            }
        }
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text
            .Replace('\u00A0', ' ')
            .Replace('\u200B', ' ')
            .Replace('\u200C', ' ')
            .Replace('\u200D', ' ')
            .Replace('\uFEFF', ' ')
            .Replace('\u2011', '-');

        text = Regex.Replace(
            text,
            @"[ \t]+",
            " ");

        text = Regex.Replace(
            text,
            @"\r\n|\r|\n",
            " ");

        return text.Trim();
    }
}