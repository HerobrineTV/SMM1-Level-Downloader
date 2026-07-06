using System.Net.Http.Json;
using System.Text.Json;
using SMMDownloader.Avalonia.Models;

namespace SMMDownloader.Avalonia.Services;

public sealed class LevelApiClient
{
    private const string AnalyticsApi = "https://api.bobac-analytics.com/smm1";
    private readonly HttpClient _httpClient = new();

    public async Task<IReadOnlyList<LevelInfo>> SearchAsync(
        AppSettings settings,
        string phrase,
        int page,
        CancellationToken cancellationToken)
    {
        var search = phrase.Trim();
        if (settings.SearchParams.LevelID &&
            !long.TryParse(search, out _) &&
            !settings.SearchParams.LevelName &&
            !settings.SearchParams.CreatorName &&
            !settings.SearchParams.CreatorID)
        {
            search = LevelCode.TryParse(search)?.ToString() ?? search;
        }

        var api = settings.ApiLink.TrimEnd('/');
        var url = $"{api}/searchLevels/{Uri.EscapeDataString(search)}/{page}" +
                  $"?coursename={(settings.SearchParams.LevelName ? 1 : 0)}" +
                  $"&courseid={(settings.SearchParams.LevelID ? 1 : 0)}" +
                  $"&creatorname={(settings.SearchParams.CreatorName ? 1 : 0)}" +
                  $"&creatorid={(settings.SearchParams.CreatorID ? 1 : 0)}" +
                  $"&searchexact={(settings.SearchParams.SearchExact ? 1 : 0)}";

        return await _httpClient.GetFromJsonAsync<List<LevelInfo>>(url, cancellationToken) ?? [];
    }

    public Task<IReadOnlyList<LevelInfo>> SearchCreatorAsync(
        AppSettings settings,
        string creatorName,
        int page,
        CancellationToken cancellationToken)
    {
        var creatorSettings = new AppSettings
        {
            ApiLink = settings.ApiLink,
            SearchParams = new SearchParams
            {
                CreatorName = true,
                SearchExact = true
            }
        };

        return SearchAsync(creatorSettings, creatorName, page, cancellationToken);
    }

    public async Task<LevelInfo?> GetLevelAsync(
        AppSettings settings,
        long levelId,
        CancellationToken cancellationToken)
    {
        var api = settings.ApiLink.TrimEnd('/');
        var url = $"{api}/level/{levelId}";
        await using var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        return root.ValueKind switch
        {
            JsonValueKind.Object => root.Deserialize<LevelInfo>(JsonStore.JsonOptions),
            JsonValueKind.Array when root.GetArrayLength() > 0 => root[0].Deserialize<LevelInfo>(JsonStore.JsonOptions),
            _ => null
        };
    }

    public async Task<IReadOnlyList<LevelInfo>> RandomAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var random = Random.Shared.Next(18118278);
        var api = settings.ApiLink.TrimEnd('/');
        var url = $"{api}/searchRandomLevels/{random}";
        return await _httpClient.GetFromJsonAsync<List<LevelInfo>>(url, cancellationToken) ?? [];
    }

    public Task RegisterFirstStartAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        return RegisterAnalyticsEventAsync($"{AnalyticsApi}/firststart", cancellationToken);
    }

    public Task RegisterLevelDownloadAsync(AppSettings settings, long levelId, CancellationToken cancellationToken)
    {
        return RegisterAnalyticsEventAsync($"{AnalyticsApi}/countdownload/{levelId}", cancellationToken);
    }

    private async Task RegisterAnalyticsEventAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
