using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;

namespace SMMDownloader.Avalonia.Models;

public sealed class SavedLevelNode
{
    public string DisplayName { get; init; } = "";
    public string Summary { get; init; } = "";
    public string ShortInfo { get; init; } = "";
    public Bitmap? Thumbnail { get; init; }
    public bool HasThumbnail => Thumbnail != null;
    public LevelInfo? Level { get; init; }
    public ObservableCollection<SavedLevelNode> Children { get; init; } = [];
}
