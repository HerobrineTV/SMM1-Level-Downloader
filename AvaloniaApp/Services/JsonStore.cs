using System.Text.Json;
using SMMDownloader.Avalonia.Models;

namespace SMMDownloader.Avalonia.Services;

public sealed class JsonStore(ProjectPaths paths)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public AppSettings LoadSettings()
    {
        if (!File.Exists(paths.SettingsFile))
        {
            return new AppSettings();
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(paths.SettingsFile), JsonOptions);
        return settings ?? new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        File.WriteAllText(paths.SettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public Dictionary<string, LevelInfo> LoadDownloaded()
    {
        return LoadDictionary<LevelInfo>(paths.DownloadedFile);
    }

    public void SaveDownloaded(Dictionary<string, LevelInfo> levels)
    {
        File.WriteAllText(paths.DownloadedFile, JsonSerializer.Serialize(levels, JsonOptions));
    }

    public Dictionary<string, string> LoadLevelPacks()
    {
        return LoadDictionary<string>(paths.LevelPacksFile);
    }

    public void SaveLevelPacks(Dictionary<string, string> packs)
    {
        File.WriteAllText(paths.LevelPacksFile, JsonSerializer.Serialize(packs, JsonOptions));
    }

    public static IReadOnlyList<LevelInfo> ToLevelList(Dictionary<string, LevelInfo> source)
    {
        return source.Values
            .OrderBy(level => string.IsNullOrWhiteSpace(level.Name) ? level.LevelId.ToString() : level.Name)
            .ToList();
    }

    private static Dictionary<string, T> LoadDictionary<T>(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Dictionary<string, T>>(text, JsonOptions) ?? [];
    }
}
