using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using News2Video.Models;

namespace News2Video.Services;

public class YandexDiskUploader
{
    private const string ApiBaseUrl =
        "https://cloud-api.yandex.net/v1/disk";

    // Папка внутри изолированного пространства приложения.
    private const string UploadDirectory =
        "app:/News2Video";

    private readonly HttpClient _httpClient;

    public YandexDiskUploader()
    {
        var token = Environment.GetEnvironmentVariable(
            "YANDEX_DISK_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Не найден YANDEX_DISK_TOKEN в .env");
        }

        _httpClient = new HttpClient();

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("OAuth", token);
    }

    public async Task UploadAsync(
        VideoFile video,
        CancellationToken cancellationToken = default)
    {
        if (video == null)
            throw new ArgumentNullException(nameof(video));

        if (string.IsNullOrWhiteSpace(video.FilePath))
        {
            throw new ArgumentException(
                "У видео не указан путь к файлу",
                nameof(video));
        }

        if (!File.Exists(video.FilePath))
        {
            throw new FileNotFoundException(
                $"Видео не найдено: {video.FilePath}");
        }

        await CreateDirectoryAsync(cancellationToken);

        var remotePath =
            $"{UploadDirectory}/{video.FileName}";

        Console.WriteLine(
            $"Загрузка на Яндекс Диск: {remotePath}");

        var uploadUrl = await GetUploadUrlAsync(
            remotePath,
            cancellationToken);

        await UploadFileAsync(
            uploadUrl,
            video.FilePath,
            cancellationToken);

        Console.WriteLine(
            $"Загрузка завершена: {remotePath}");
    }

    private async Task CreateDirectoryAsync(
        CancellationToken cancellationToken)
    {
        var encodedPath =
            Uri.EscapeDataString(UploadDirectory);

        var url =
            $"{ApiBaseUrl}/resources" +
            $"?path={encodedPath}";

        using var response = await _httpClient.PutAsync(
            url,
            content: null,
            cancellationToken);

        if (response.IsSuccessStatusCode)
            return;

        // Папка уже существует.
        if (response.StatusCode == HttpStatusCode.Conflict)
            return;

        var error =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        throw new InvalidOperationException(
            $"Не удалось создать папку на Яндекс Диске" +
            $"HTTP {(int)response.StatusCode}: {error}");
    }

    private async Task<string> GetUploadUrlAsync(
        string remotePath,
        CancellationToken cancellationToken)
    {
        var encodedPath =
            Uri.EscapeDataString(remotePath);

        var url =
            $"{ApiBaseUrl}/resources/upload" +
            $"?path={encodedPath}" +
            "&overwrite=true";

        using var response = await _httpClient.GetAsync(
            url,
            cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Не удалось получить ссылку для загрузки" +
                $"HTTP {(int)response.StatusCode}: {responseBody}");
        }

        using var json =
            JsonDocument.Parse(responseBody);

        if (!json.RootElement.TryGetProperty(
                "href",
                out var hrefElement))
        {
            throw new InvalidOperationException(
                "В ответе Яндекс Диска нет поля href");
        }

        var href = hrefElement.GetString();

        if (string.IsNullOrWhiteSpace(href))
        {
            throw new InvalidOperationException(
                "Яндекс Диск вернул пустой URL загрузки");
        }

        return href;
    }

    private async Task UploadFileAsync(
        string uploadUrl,
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        using var content = new StreamContent(fileStream);

        content.Headers.ContentType =
            new MediaTypeHeaderValue("video/mp4");

        using var response = await _httpClient.PutAsync(
            uploadUrl,
            content,
            cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Ошибка загрузки файла на Яндекс Диск" +
                $"HTTP {(int)response.StatusCode}: {responseBody}");
        }
    }
}