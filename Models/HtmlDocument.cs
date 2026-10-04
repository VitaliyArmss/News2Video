namespace News2Video.Models;

public sealed class HtmlDocument
{
    public required string Url { get; init; }
    public required string Content { get; init; }
    public DateTime ReceivedAt { get; init; }
}
