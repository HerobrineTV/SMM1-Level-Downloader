using System.Text.Json.Serialization;

namespace SMMDownloader.Avalonia.Models;

public sealed class AppSettings
{
    [JsonPropertyName("useCemuDir")]
    public bool UseCemuDir { get; set; }

    [JsonPropertyName("BackupLevels")]
    public bool BackupLevels { get; set; }

    [JsonPropertyName("CemuDirPath")]
    public string CemuDirPath { get; set; } = "";

    [JsonPropertyName("selectedProfile")]
    public string SelectedProfile { get; set; } = "";

    [JsonPropertyName("useAPILink")]
    public bool UseApiLink { get; set; } = true;

    [JsonPropertyName("APILink")]
    public string ApiLink { get; set; } = "https://api.bobac-analytics.com/smm1";

    [JsonPropertyName("lastSearchPhrase")]
    public string LastSearchPhrase { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("recentFoundLevels")]
    public Dictionary<string, LevelInfo> RecentFoundLevels { get; set; } = [];

    [JsonPropertyName("useProxy")]
    public bool UseProxy { get; set; }

    [JsonPropertyName("hideViewerInfo")]
    public bool HideViewerInfo { get; set; }

    [JsonPropertyName("firstStartRegistered")]
    public bool FirstStartRegistered { get; set; }

    [JsonPropertyName("lastFullRefresh")]
    public string LastFullRefresh { get; set; } = "";

    [JsonPropertyName("hidePrereleaseWarning")]
    public bool HidePrereleaseWarning { get; set; }

    [JsonPropertyName("searchParams")]
    public SearchParams SearchParams { get; set; } = new();
}

public sealed class SearchParams
{
    public bool LevelName { get; set; }
    public bool LevelID { get; set; }
    public bool CreatorName { get; set; }
    public bool CreatorID { get; set; }
    public bool SearchExact { get; set; }
}
