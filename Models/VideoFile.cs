namespace News2Video.Models;

public sealed class VideoFile
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public TimeSpan? Duration { get; init; }
}
