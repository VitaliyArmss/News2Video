using System.Diagnostics;
using System.Globalization;
using System.Text;
using News2Video.Models;

namespace News2Video.Services;

public class VideoEditor
{
    private const string FfmpegRelativePath = "tools/ffmpeg.exe";
    private const string BackgroundRelativePath = "assets/background.mp4";
    private const string MusicRelativePath = "assets/music.mp3";
    private const string FontRelativePath = "assets/font.ttf";
    private const string OutputDirectory = "data/videos";

    public async Task<VideoFile> CreateVideoAsync(
        NewsContent news,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(news.Title))
            throw new ArgumentException(
                "У новости отсутствует заголовок.",
                nameof(news));

        if (string.IsNullOrWhiteSpace(news.Text))
            throw new ArgumentException(
                "У новости отсутствует текст.",
                nameof(news));

        var ffmpegPath = GetAssetsPath(FfmpegRelativePath);
        var backgroundPath = GetAssetsPath(BackgroundRelativePath);
        var musicPath = GetAssetsPath(MusicRelativePath);
        var fontPath = GetAssetsPath(FontRelativePath);

        CheckFile(ffmpegPath, "FFmpeg");
        CheckFile(backgroundPath, "Фоновое видео");
        CheckFile(musicPath, "Музыка");
        CheckFile(fontPath, "Шрифт");

        var outputDirectory = GetOutputPath(OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var fileName = $"{CreateSafeFileName(news.Title)}.mp4";
        var outputPath = Path.Combine(outputDirectory, fileName);

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "News2Video",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempDirectory);

        var titlePath = Path.Combine(tempDirectory, "title.txt");
        var textPath = Path.Combine(tempDirectory, "text.txt");

        try
        {
            await File.WriteAllTextAsync(
                titlePath,
                PrepareText(news.Title, 35),
                new UTF8Encoding(false),
                cancellationToken);

            await File.WriteAllTextAsync(
                textPath,
                PrepareText(news.Text, 55),
                new UTF8Encoding(false),
                cancellationToken);

            var duration = TimeSpan.FromSeconds(15);

            var arguments = new List<string>
            {
                "-y",

                // Фоновое видео зацикливаем
                "-stream_loop", "-1",
                "-i", backgroundPath,

                // Музыку тоже зацикливаем
                "-stream_loop", "-1",
                "-i", musicPath,

                "-map", "0:v:0",   // видео из фона
                "-map", "1:a:0",   // аудио из музыки

                // Длительность итогового видео
                "-t",
                duration.TotalSeconds.ToString(
                    CultureInfo.InvariantCulture),

                // Текст поверх видео
                "-vf",
                BuildVideoFilter(
                    fontPath,
                    titlePath,
                    textPath),

                // Видео
                "-c:v", "libx264",
                "-preset", "medium",
                "-crf", "23",
                "-pix_fmt", "yuv420p",

                // Аудио
                "-c:a", "aac",
                "-b:a", "192k",

                "-shortest",

                outputPath
            };

            await RunFfmpegAsync(
                ffmpegPath,
                arguments,
                cancellationToken);

            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException(
                    "FFmpeg не создал выходной видеофайл.");
            }

            return new VideoFile
            {
                FilePath = outputPath,
                FileName = fileName,
                Duration = duration
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, true);
            }
            catch
            {
                // Ошибка удаления временных файлов
                // не должна ломать основной процесс
            }
        }
    }

    private static string BuildVideoFilter(
        string fontPath,
        string titlePath,
        string textPath)
    {
        var font = EscapeFilterPath(fontPath);
        var title = EscapeFilterPath(titlePath);
        var text = EscapeFilterPath(textPath);

        return
            // Приводим фон к Full HD.
            "scale=1080:1920:force_original_aspect_ratio=increase," +
            "crop=1080:1920," +

            // Затемняем фон, чтобы текст был хорошо виден
            "drawbox=" +
            "x=0:y=0:w=iw:h=ih:" +
            "color=black@0.70:" +
            "t=fill," +

            // Заголовок
            "drawtext=" +
            $"fontfile='{font}':" +
            $"textfile='{title}':" +
            "fontcolor=white:" +
            "fontsize=48:" +
            "line_spacing=3:" +
            "x=(w-text_w)/2:" +
            "y=100:" +
            "text_align=center:" +
            "box=1:" +
            "boxcolor=black@0.35:" +
            "expansion=none:" +
            "boxborderw=20," +

            // Основной текст
            "drawtext=" +
            $"fontfile='{font}':" +
            $"textfile='{text}':" +
            "fontcolor=white:" +
            "fontsize=34:" +
            "line_spacing=3:" +
            "x=(w-text_w)/2:" +
            "y=400:" +
            "text_align=center:" +
            "box=1:" +
            "expansion=none:" +
            "boxcolor=black@0.25:" +
            "boxborderw=15";
    }

    private static async Task RunFfmpegAsync(
        string ffmpegPath,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Не удалось запустить FFmpeg.");
        }

        var errorTask = process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFmpeg завершился с ошибкой " +
                $"{process.ExitCode}:{Environment.NewLine}{error}");
        }
    }

    private static string PrepareText(
        string text,
        int maxCharactersPerLine)
    {
        text = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();

        var result = new StringBuilder();

        var paragraphs = text.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var paragraph in paragraphs)
        {
            var words = paragraph.Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length == 0)
                {
                    currentLine.Append(word);
                    continue;
                }

                if (currentLine.Length + word.Length + 1
                    <= maxCharactersPerLine)
                {
                    currentLine.Append(' ');
                    currentLine.Append(word);
                }
                else
                {
                    result.Append(currentLine);
                    result.Append('\n');
                    currentLine.Clear();
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
                result.Append(currentLine);
            result.Append('\n');
        }

        return result.ToString().Trim();
    }

    private static string CreateSafeFileName(string title)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();

        var result = new string(
            title
                .Select(character =>
                    invalidCharacters.Contains(character)
                        ? '_'
                        : character)
                .ToArray());

        result = result.Trim();

        if (result.Length > 100)
            result = result[..100];

        return string.IsNullOrWhiteSpace(result)
            ? $"video_{DateTime.Now:yyyyMMdd_HHmmss}"
            : result;
    }

    private static string GetAssetsPath(string relativePath)
    {
        var projectDirectory = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                ".."));

        return Path.Combine(
            projectDirectory,
            relativePath);
    }

    private static string GetOutputPath(string relativePath)
    {
        var projectDirectory = AppContext.BaseDirectory;

        return Path.Combine(
            projectDirectory,
            relativePath);
    }

    private static void CheckFile(
        string path,
        string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{description} не найден: {path}");
        }
    }

    private static string EscapeFilterPath(string path)
    {
        return path
            .Replace("\\", "/")
            .Replace(":", "\\:")
            .Replace("'", "\\'");
    }
}