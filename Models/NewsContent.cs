namespace News2Video.Models;

public sealed class NewsContent
{
    public string Title { get; init; }
    public string Text { get; init; }
    public string Url { get; init; }
    public DateTime? PublishedAt { get; init; }
}