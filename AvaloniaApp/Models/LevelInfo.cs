using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Avalonia.Media;
using SMMDownloader.Avalonia.Services;

namespace SMMDownloader.Avalonia.Models;

public sealed class LevelInfo : INotifyPropertyChanged
{
    private bool _isDownloaded;
    private bool _isDownloading;
    private string _downloadProgressText = "";
    private IImage? _creatorMiiImage;
    private IImage? _worldRecordMiiImage;
    private IImage? _thumbnail;

    public event PropertyChangedEventHandler? PropertyChanged;

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("creator")]
    public string Creator { get; set; } = "";

    [JsonPropertyName("creator_mii_data")]
    public string? CreatorMiiData { get; set; }

    [JsonPropertyName("levelid")]
    [JsonConverter(typeof(FlexibleLongConverter))]
    public long LevelId { get; set; }

    [JsonPropertyName("creatorid")]
    [JsonConverter(typeof(FlexibleLongConverter))]
    public long CreatorId { get; set; }

    [JsonPropertyName("clears")]
    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Clears { get; set; }

    [JsonPropertyName("failures")]
    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Failures { get; set; }

    [JsonPropertyName("total_attempts")]
    [JsonConverter(typeof(FlexibleIntConverter))]
    public int TotalAttempts { get; set; }

    [JsonPropertyName("clearrate")]
    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double ClearRate { get; set; }

    [JsonPropertyName("uploadTime")]
    public string UploadTime { get; set; } = "";

    [JsonPropertyName("world_record_ms")]
    [JsonConverter(typeof(FlexibleLongConverter))]
    public long WorldRecordMs { get; set; }

    [JsonPropertyName("world_record_holder_nnid")]
    public string WorldRecordHolderNnid { get; set; } = "";

    [JsonPropertyName("world_record_best_time_player_mii_data")]
    public string? WorldRecordBestTimePlayerMiiData { get; set; }

    [JsonPropertyName("stars")]
    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Stars { get; set; }

    [JsonPropertyName("downloads")]
    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Downloads { get; set; }

    [JsonPropertyName("pack")]
    public string? Pack { get; set; }

    [JsonIgnore]
    public string Folder { get; set; } = "";

    [JsonIgnore]
    public string DisplayCode => LevelCode.Generate(LevelId);

    [JsonIgnore]
    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (SetField(ref _isDownloaded, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(DownloadButtonText));
                OnPropertyChanged(nameof(SearchResultStatus));
            }
        }
    }

    [JsonIgnore]
    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetField(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(DownloadButtonText));
                OnPropertyChanged(nameof(SearchResultStatus));
            }
        }
    }

    [JsonIgnore]
    public string DownloadProgressText
    {
        get => _downloadProgressText;
        set
        {
            if (SetField(ref _downloadProgressText, value))
            {
                OnPropertyChanged(nameof(SearchResultStatus));
            }
        }
    }

    [JsonIgnore]
    public bool CanDownload => !IsDownloaded && !IsDownloading;

    [JsonIgnore]
    public string DownloadButtonText => IsDownloaded ? "Downloaded" : IsDownloading ? "Downloading" : "Download";

    [JsonIgnore]
    public string SearchResultStatus => IsDownloaded ? "Already downloaded" : DownloadProgressText;

    [JsonIgnore]
    public IImage? CreatorMiiImage
    {
        get => _creatorMiiImage;
        set
        {
            if (SetField(ref _creatorMiiImage, value))
            {
                OnPropertyChanged(nameof(HasCreatorMiiImage));
            }
        }
    }

    [JsonIgnore]
    public bool HasCreatorMiiImage => CreatorMiiImage != null;

    [JsonIgnore]
    public IImage? WorldRecordMiiImage
    {
        get => _worldRecordMiiImage;
        set
        {
            if (SetField(ref _worldRecordMiiImage, value))
            {
                OnPropertyChanged(nameof(HasWorldRecordMiiImage));
            }
        }
    }

    [JsonIgnore]
    public bool HasWorldRecordMiiImage => WorldRecordMiiImage != null;

    [JsonIgnore]
    public IImage? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (SetField(ref _thumbnail, value))
            {
                OnPropertyChanged(nameof(HasThumbnail));
            }
        }
    }

    [JsonIgnore]
    public bool HasThumbnail => Thumbnail != null;

    [JsonIgnore]
    public string ClearRateText => $"{ClearRate * 100:0.##}%";

    [JsonIgnore]
    public string ClearsText => $"{Clears}/{TotalAttempts}";

    [JsonIgnore]
    public string TotalAttemptsText => TotalAttempts.ToString("N0", CultureInfo.InvariantCulture);

    [JsonIgnore]
    public string StarsText => Stars.ToString("N0", CultureInfo.InvariantCulture);

    [JsonIgnore]
    public string DownloadsText => Downloads.ToString("N0", CultureInfo.InvariantCulture);

    [JsonIgnore]
    public string Summary
    {
        get
        {
            var clearRate = (ClearRate * 100).ToString("0.##", CultureInfo.InvariantCulture);
            var pack = string.IsNullOrWhiteSpace(Pack) ? "" : $" | Pack: {Pack}";
            return $"{DisplayCode} ({LevelId}) | {Creator} | {clearRate}% ({Clears}/{TotalAttempts}) | Stars: {Stars} | Downloads: {DownloadsText}{pack}";
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
