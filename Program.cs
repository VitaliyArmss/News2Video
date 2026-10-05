using News2Video.Services;

const string rssUrl = "https://3dnews.ru/news/rss/";
const string processedFile = "data/processed.txt";

using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30)
};

LoadEnv();

var rssWorker = new RssWorker(httpClient);
var webWorker = new WebWorker(httpClient);
var parser = new Parser();
var videoEditor = new VideoEditor();
var uploader = new YandexDiskUploader();

Directory.CreateDirectory("data");
Directory.CreateDirectory("data/raw");

using var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
    Console.WriteLine("Остановка...");
};

while (!cancellationTokenSource.IsCancellationRequested)
{
    try
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Проверяем RSS...");

        var articles = await rssWorker.GetLatestAsync(
            rssUrl,
            cancellationTokenSource.Token);

        foreach (var article in articles.OrderBy(x => x.PublishedAt))
        {
            if (IsProcessed(article.Url))
                continue;

            Console.WriteLine($"Новая статья: {article.Title}");
            Console.WriteLine(article.Url);

            var html = await webWorker.GetHtmlAsync(
                article.Url,
                cancellationTokenSource.Token);

            var rawPath = Path.Combine(
                "data",
                "raw",
                $"article_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.html");

            await File.WriteAllTextAsync(
                rawPath,
                html.Content,
                cancellationTokenSource.Token);

            var news = await parser.ParseAsync(
                html,
                cancellationTokenSource.Token);

            Console.WriteLine($"Заголовок: {news.Title}");
            Console.WriteLine($"Текст: {news.Text[..Math.Min(news.Text.Length, 200)]}...");

            try
            {
                var video = await videoEditor.CreateVideoAsync(
                    news,
                    cancellationTokenSource.Token);

                await uploader.UploadAsync(
                    video,
                    cancellationTokenSource.Token);

                MarkAsProcessed(article.Url);
            }
            catch (NotImplementedException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Статья пока НЕ помечена как обработанная");
                break;
            }
        }
    }
    catch (OperationCanceledException) when (
        cancellationTokenSource.IsCancellationRequested)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
    }

    try
    {
        Console.WriteLine("Следующая проверка через 5 минут");
        await Task.Delay(
            TimeSpan.FromMinutes(5),
            cancellationTokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

bool IsProcessed(string url)
{
    if (!File.Exists(processedFile))
        return false;

    return File.ReadLines(processedFile)
        .Any(line => string.Equals(
            line.Trim(),
            url,
            StringComparison.OrdinalIgnoreCase));
}

void MarkAsProcessed(string url)
{
    File.AppendAllText(
        processedFile,
        url + Environment.NewLine);
}

static void LoadEnv()
{
    var projectDirectory = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            ".."));

    var envPath = Path.Combine(
        projectDirectory,
        ".env");

    if (!File.Exists(envPath))
    {
        throw new FileNotFoundException(
            $"Файл .env не найден: {envPath}");
    }

    foreach (var rawLine in File.ReadLines(envPath))
    {
        var line = rawLine.Trim();

        // Пустая строка
        if (string.IsNullOrWhiteSpace(line))
            continue;

        // Комментарий
        if (line.StartsWith('#'))
            continue;

        var separatorIndex = line.IndexOf('=');

        if (separatorIndex <= 0)
            continue;

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        // Убираем кавычки:
        // KEY="value"
        // KEY='value'
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        Environment.SetEnvironmentVariable(
            key,
            value);
    }
}