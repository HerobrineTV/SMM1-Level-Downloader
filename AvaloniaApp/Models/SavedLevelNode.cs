using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SMMDownloader.Avalonia.Models;

public sealed class SavedLevelNode : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isMultiSelected;
    private bool _isMultiSelectMode;
    private bool _statsHidden;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; init; } = "";
    public string Summary { get; init; } = "";
    public string ShortInfo { get; init; } = "";
    public Bitmap? Thumbnail { get; init; }
    public bool HasThumbnail => Thumbnail != null;
    public bool NoThumbnail => Thumbnail == null;
    public bool IsFolder { get; init; }
    public bool IsProtectedFolder { get; init; }
    public bool CanDeleteFolder => IsFolder && !IsProtectedFolder;
    public Thickness RowMargin { get; init; } = new(0, 5, 0, 5);
    public LevelInfo? Level { get; init; }
    public ObservableCollection<SavedLevelNode> Children { get; init; } = [];
    public string? PackName { get; init; }
    public string? PackFolder { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowStats));
        }
    }

    public bool IsMultiSelected
    {
        get => _isMultiSelected;
        set
        {
            if (_isMultiSelected == value)
            {
                return;
            }

            _isMultiSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RowBackground));
            OnPropertyChanged(nameof(RowBorderBrush));
        }
    }

    public bool IsMultiSelectMode
    {
        get => _isMultiSelectMode;
        set
        {
            if (_isMultiSelectMode == value)
            {
                return;
            }

            _isMultiSelectMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowMultiSelectCheckBox));
        }
    }

    public bool StatsHidden
    {
        get => _statsHidden;
        set
        {
            if (_statsHidden == value)
            {
                return;
            }

            _statsHidden = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowStats));
        }
    }

    public bool ShowStats => Level != null && !StatsHidden;
    public bool CanMultiSelect => Level != null;
    public bool ShowMultiSelectCheckBox => IsMultiSelectMode && CanMultiSelect;
    public IBrush RowBackground => IsMultiSelected ? new SolidColorBrush(Color.FromRgb(255, 216, 106)) : Brushes.Transparent;
    public IBrush RowBorderBrush => IsMultiSelected ? new SolidColorBrush(Color.FromRgb(143, 79, 23)) : Brushes.Transparent;
    public string CodeText => Level?.DisplayCode ?? "";
    public string ClearRateText => Level?.ClearRateText ?? "";
    public string AttemptsText => Level?.TotalAttemptsText ?? "";
    public string StarsText => Level?.StarsText ?? "";
    public string DownloadsText => Level?.DownloadsText ?? "";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
