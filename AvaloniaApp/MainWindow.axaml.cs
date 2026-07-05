using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SMMDownloader.Avalonia.Controls;
using SMMDownloader.Avalonia.Models;
using SMMDownloader.Avalonia.Services;

namespace SMMDownloader.Avalonia;

public sealed partial class MainWindow : Window
{
    private const string ApiPingUrl = "https://api.bobac-analytics.com/smm1/ping";
    private const string GithubLatestReleaseUrl = "https://api.github.com/repos/HerobrineTV/SMM1-Level-Downloader/releases/latest";
    private const string CurrentReleaseTag = "V1.0.0";

    private readonly ProjectPaths _paths = new();
    private readonly JsonStore _store;
    private readonly DataMigrationService _dataMigrationService;
    private readonly LevelApiClient _apiClient = new();
    private readonly LevelDownloadService _downloadService;
    private readonly SmmCourseParser _courseParser = new();
    private readonly ThumbnailLoader _thumbnailLoader = new();
    private readonly MiiImageLoader _miiImageLoader = new();
    private readonly HttpClient _statusHttpClient = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly ObservableCollection<LevelInfo> _searchResults = [];
    private readonly ObservableCollection<LevelInfo> _profileLevels = [];
    private readonly ObservableCollection<SavedLevelNode> _savedNodes = [];
    private readonly Dictionary<long, DownloadState> _downloadStates = [];
    private IReadOnlyList<LevelInfo> _allSavedLevels = [];
    private IReadOnlyList<SavedLevelNode> _allSavedNodes = [];
    private AppSettings _settings = new();
    private CancellationTokenSource? _workCts;
    private string _currentSearchPhrase = "";
    private int _currentSearchPage = 1;
    private bool _isLoadingSearchPage;
    private bool _hasMoreSearchPages;
    private string _currentProfileUserName = "";
    private LevelInfo? _selectedPreviewLevel;
    private SavedLevelNode? _selectedSavedNode;
    private SavedLevelNode? _selectedPackNode;
    private string _selectedPreviewFile = "course_data.cdt";
    private LevelInfo? _largeViewerLevel;
    private string _largeViewerFile = "course_data.cdt";
    private bool _returnToProfilePageFromViewer;
    private bool? _isApiOnline;
    private string? _availableUpdateVersion;
    private string? _availableUpdateUrl;
    private bool _startupInitialized;
    private bool _isApplyingPackNameSuggestion;

    public MainWindow()
    {
        InitializeComponent();

        _store = new JsonStore(_paths);
        _dataMigrationService = new DataMigrationService(_paths);
        _downloadService = new LevelDownloadService(_paths, _store);
        SearchResultsListBox.ItemsSource = _searchResults;
        ProfileLevelsListBox.ItemsSource = _profileLevels;
        SavedLevelsTreeView.ItemsSource = _savedNodes;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (_startupInitialized)
        {
            return;
        }

        _startupInitialized = true;
        await RunStartupMigrationAsync();
        _store.EnsureInitialized();
        InitializeDataBackedUi();
    }

    private void InitializeDataBackedUi()
    {
        LoadSettingsIntoUi();
        ApplyLanguage();
        RefreshPackNameSuggestions();
        ResetSearchSelectedLevelDetails();
        ResetProfileSelectedLevelDetails();
        LoadSavedLevels();
        _ = RunApiStatusLoopAsync(_lifetimeCts.Token);
        _ = CheckForUpdatesAsync(_lifetimeCts.Token);
    }

    private async Task RunStartupMigrationAsync()
    {
        if (!_dataMigrationService.NeedsLegacyDataMigration())
        {
            return;
        }

        await ShowLegacyDataMigrationPromptAsync();

        try
        {
            SetBusy("Moving legacy data...");
            await _dataMigrationService.MigrateLegacyDataAsync(_lifetimeCts.Token);
            SetStatus($"Data moved to {_paths.DataDirectory}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Legacy data migration failed: {ex.Message}");
        }
    }

    private Task ShowLegacyDataMigrationPromptAsync()
    {
        var dialog = new Window
        {
            Title = "Data Migration",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FFF1AE")),
                BorderBrush = new SolidColorBrush(Color.Parse("#7B4A20")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Old Data Path Detected! Migrating the Data now!",
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#2B1605"))
                        },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = HorizontalAlignment.Right,
                            MinWidth = 90,
                            Padding = new Thickness(13, 8),
                            Background = new SolidColorBrush(Color.Parse("#F9C74F")),
                            Foreground = new SolidColorBrush(Color.Parse("#2B1605")),
                            BorderBrush = new SolidColorBrush(Color.Parse("#8F4F17")),
                            BorderThickness = new Thickness(2),
                            CornerRadius = new CornerRadius(5)
                        }
                    }
                }
            }
        };

        if (dialog.Content is Border { Child: StackPanel panel } && panel.Children[^1] is Button okButton)
        {
            okButton.Click += (_, _) => dialog.Close();
        }

        return dialog.ShowDialog(this);
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string message, string confirmText, string cancelText)
    {
        var confirmButton = new Button
        {
            Content = confirmText,
            MinWidth = 90,
            Padding = new Thickness(13, 8),
            Background = new SolidColorBrush(Color.Parse("#F9C74F")),
            Foreground = new SolidColorBrush(Color.Parse("#2B1605")),
            BorderBrush = new SolidColorBrush(Color.Parse("#8F4F17")),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5)
        };
        var cancelButton = new Button
        {
            Content = cancelText,
            MinWidth = 90,
            Padding = new Thickness(13, 8),
            Background = new SolidColorBrush(Color.Parse("#FFF8D6")),
            Foreground = new SolidColorBrush(Color.Parse("#2B1605")),
            BorderBrush = new SolidColorBrush(Color.Parse("#8F4F17")),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5)
        };
        var dialog = new Window
        {
            Title = title,
            Width = 500,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FFF1AE")),
                BorderBrush = new SolidColorBrush(Color.Parse("#7B4A20")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#2B1605"))
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelButton, confirmButton }
                        }
                    }
                }
            }
        };

        cancelButton.Click += (_, _) => dialog.Close(false);
        confirmButton.Click += (_, _) => dialog.Close(true);
        return await dialog.ShowDialog<bool>(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _statusHttpClient.Dispose();
        base.OnClosed(e);
    }

    private async void SearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync(async token =>
        {
            SaveSettingsFromUi();
            var phrase = SearchTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(phrase))
            {
                SetStatus("Enter a search phrase.");
                return;
            }

            SetBusy("Searching courses...");
            var results = await _apiClient.SearchAsync(_settings, phrase, 1, token);
            _searchResults.Clear();
            foreach (var level in results)
            {
                AddSearchResult(level);
            }

            _currentSearchPhrase = phrase;
            _currentSearchPage = 1;
            _hasMoreSearchPages = results.Count > 0;
            _settings.LastSearchPhrase = phrase;
            _settings.RecentFoundLevels = results.ToDictionary(level => level.LevelId.ToString());
            _store.SaveSettings(_settings);
            SetStatus($"Found {_searchResults.Count} courses.");
        });
    }

    private async void RandomButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync(async token =>
        {
            SaveSettingsFromUi();
            SetBusy("Loading random course...");
            var results = await _apiClient.RandomAsync(_settings, token);
            _searchResults.Clear();
            foreach (var level in results)
            {
                AddSearchResult(level);
            }

            _currentSearchPhrase = "";
            _currentSearchPage = 1;
            _hasMoreSearchPages = false;
            SetStatus($"Loaded {_searchResults.Count} random result(s).");
        });
    }

    private async void SearchResultsListBox_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!_hasMoreSearchPages || _isLoadingSearchPage || string.IsNullOrWhiteSpace(_currentSearchPhrase))
        {
            return;
        }

        if (e.ExtentDelta.Y == 0 && e.OffsetDelta.Y <= 0)
        {
            return;
        }

        var scrollViewer = (e.Source as ScrollViewer) ??
                           SearchResultsListBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer == null)
        {
            return;
        }

        if (scrollViewer.Offset.Y + scrollViewer.Viewport.Height < scrollViewer.Extent.Height - 180)
        {
            return;
        }

        await LoadNextSearchPageAsync();
    }

    private async Task LoadNextSearchPageAsync()
    {
        try
        {
            _isLoadingSearchPage = true;
            SaveSettingsFromUi();
            var nextPage = _currentSearchPage + 1;
            SetStatus($"Loading search page {nextPage}...");
            var results = await _apiClient.SearchAsync(_settings, _currentSearchPhrase, nextPage, CancellationToken.None);
            if (results.Count == 0)
            {
                _hasMoreSearchPages = false;
                SetStatus($"Loaded all {_searchResults.Count} search results.");
                return;
            }

            var existingIds = _searchResults.Select(level => level.LevelId).ToHashSet();
            var added = 0;
            foreach (var level in results.Where(level => existingIds.Add(level.LevelId)))
            {
                AddSearchResult(level);
                added++;
            }

            if (added == 0)
            {
                _hasMoreSearchPages = false;
                SetStatus($"Loaded all {_searchResults.Count} search results.");
                return;
            }

            _currentSearchPage = nextPage;
            SetStatus($"Loaded {_searchResults.Count} courses.");
        }
        catch (Exception ex)
        {
            _hasMoreSearchPages = false;
            SetStatus(ex.Message);
        }
        finally
        {
            _isLoadingSearchPage = false;
        }
    }

    private async Task OpenProfileAsync(string? userName, IImage? miiImage)
    {
        userName = userName?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        await RunSafeAsync(async token =>
        {
            SetBusy($"Loading profile for {userName}...");
            MainTabs.IsVisible = false;
            ProfilePage.IsVisible = true;
            _currentProfileUserName = userName;
            ProfileUserNameText.Text = userName;
            ProfileHeaderMiiImage.Source = miiImage;
            ProfileSummaryText.Text = $"Loading levels for {userName}...";
            ResetProfileStats();
            _profileLevels.Clear();

            var existingIds = new HashSet<long>();
            var page = 1;
            while (true)
            {
                SetStatus($"Loading profile page {page} for {userName}...");
                var results = await _apiClient.SearchCreatorAsync(_settings, userName, page, token);
                if (results.Count == 0)
                {
                    break;
                }

                var added = 0;
                foreach (var level in results.Where(level => existingIds.Add(level.LevelId)))
                {
                    AddProfileLevel(level);
                    added++;
                }

                ProfileSummaryText.Text = $"{userName}: {_profileLevels.Count} level(s) loaded.";
                if (added == 0)
                {
                    break;
                }

                page++;
            }

            ProfileSummaryText.Text = _profileLevels.Count == 0
                ? $"No levels found for {userName}."
                : $"{userName}: {_profileLevels.Count} level(s) loaded.";
            UpdateProfileStats();
            SetStatus($"Loaded {_profileLevels.Count} profile level(s).");
        });
    }

    private async void CreatorMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is LevelInfo level)
        {
            await OpenProfileAsync(level.Creator, level.CreatorMiiImage);
            e.Handled = true;
        }
    }

    private async void WorldRecordMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is LevelInfo level)
        {
            await OpenProfileAsync(level.WorldRecordHolderNnid, level.WorldRecordMiiImage);
            e.Handled = true;
        }
    }

    private async void SelectedCreatorMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is LevelInfo level)
        {
            await OpenProfileAsync(level.Creator, level.CreatorMiiImage);
            e.Handled = true;
        }
    }

    private async void SelectedWorldRecordMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is LevelInfo level)
        {
            await OpenProfileAsync(level.WorldRecordHolderNnid, level.WorldRecordMiiImage);
            e.Handled = true;
        }
    }

    private async void SavedCreatorMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_selectedPreviewLevel is { } level)
        {
            await OpenProfileAsync(level.Creator, level.CreatorMiiImage);
            e.Handled = true;
        }
    }

    private async void SavedWorldRecordMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_selectedPreviewLevel is { } level)
        {
            await OpenProfileAsync(level.WorldRecordHolderNnid, level.WorldRecordMiiImage);
            e.Handled = true;
        }
    }

    private async void ProfileSelectedCreatorMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ProfileLevelsListBox.SelectedItem is LevelInfo level)
        {
            await OpenProfileAsync(level.Creator, level.CreatorMiiImage);
            e.Handled = true;
        }
    }

    private async void ProfileSelectedWorldRecordMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ProfileLevelsListBox.SelectedItem is LevelInfo level)
        {
            await OpenProfileAsync(level.WorldRecordHolderNnid, level.WorldRecordMiiImage);
            e.Handled = true;
        }
    }

    private void SearchThumbnail_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is LevelInfo level)
        {
            SearchResultsListBox.SelectedItem = level;
            OpenLargeLevelViewer(level, false);
            e.Handled = true;
        }
    }

    private void ProfileThumbnail_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is LevelInfo level)
        {
            ProfileLevelsListBox.SelectedItem = level;
            OpenLargeLevelViewer(level, true);
            e.Handled = true;
        }
    }

    private void SearchSelectedPreviewPanel_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is LevelInfo level)
        {
            OpenLargeLevelViewer(level, false);
            e.Handled = true;
        }
    }

    private void ProfileSelectedPreviewPanel_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ProfileLevelsListBox.SelectedItem is LevelInfo level)
        {
            OpenLargeLevelViewer(level, true);
            e.Handled = true;
        }
    }

    private void BackFromProfileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ProfilePage.IsVisible = false;
        MainTabs.IsVisible = true;
    }

    private void BackFromViewerButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LevelViewerPage.IsVisible = false;
        if (_returnToProfilePageFromViewer)
        {
            ProfilePage.IsVisible = true;
        }
        else
        {
            MainTabs.IsVisible = true;
        }
    }

    private void LevelViewerOverworldButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_largeViewerLevel == null)
        {
            return;
        }

        _largeViewerFile = "course_data.cdt";
        LoadLargeLevelViewer(_largeViewerLevel, _largeViewerFile);
    }

    private void LevelViewerUnderworldButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_largeViewerLevel == null)
        {
            return;
        }

        _largeViewerFile = "course_data_sub.cdt";
        LoadLargeLevelViewer(_largeViewerLevel, _largeViewerFile);
    }

    private void LevelViewerHiddenBlocksButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LevelViewerCanvas.ShowHiddenBlocks = !LevelViewerCanvas.ShowHiddenBlocks;
        LevelViewerHiddenBlocksButton.Content = LevelViewerCanvas.ShowHiddenBlocks
            ? T("HideHiddenBlocks")
            : T("RevealHiddenBlocks");
    }

    private void ProfileLevelsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProfileLevelsListBox.SelectedItem is not LevelInfo level)
        {
            ResetProfileSelectedLevelDetails();
            return;
        }

        ShowProfileSelectedLevelContainer();
        ProfileSelectedDetailsPanel.IsVisible = true;
        ProfileSelectedLevelTitle.Text = $"{level.Name} | {level.DisplayCode}";
        ProfileSelectedCreatorNameText.Text = FormatName(level.Creator);
        ProfileSelectedWorldRecordText.Text = level.WorldRecordMs > 0
            ? $"{FormatTime(level.WorldRecordMs)} by {FormatName(level.WorldRecordHolderNnid)}"
            : "n/a";
        ProfileSelectedLevelDetails.Text =
            $"Upload: {FormatUploadTime(level.UploadTime)}";
        SetProfileSelectedStats(level);
        ShowInlineCoursePreview(level, ProfileSelectedPreviewCanvas, ProfileSelectedPreviewPanel);
        ShowSelectedProfileMiiImages(level);
        _ = LoadProfileLevelMiiImagesAsync(level);
    }

    private void ResetProfileStats()
    {
        ProfileTotalLevelsText.Text = "Levels: 0";
        ProfileTotalStarsText.Text = "Stars: 0";
        ProfileTotalAttemptsText.Text = "Attempts: 0";
        ProfileTotalClearsText.Text = "Clears: 0";
        ProfileAverageClearRateText.Text = "Clear Rate: n/a";
        ProfileSelectedLevelTitle.Text = T("NoLevelSelected");
        ResetProfileSelectedLevelDetails();
    }

    private void ResetProfileSelectedLevelDetails()
    {
        CollapseProfileSelectedLevelContainer();
        ProfileSelectedLevelContainer.IsVisible = false;
        ProfileSelectedLevelTitle.Text = T("NoLevelSelected");
        ProfileSelectedDetailsPanel.IsVisible = false;
        ProfileSelectedLevelDetails.Text = "";
        ProfileSelectedCreatorRow.IsVisible = false;
        ProfileSelectedWorldRecordRow.IsVisible = false;
        ProfileSelectedPreviewPanel.IsVisible = false;
    }

    private void ResetSearchSelectedLevelDetails()
    {
        CollapseSearchSelectedLevelContainer();
        SearchSelectedLevelContainer.IsVisible = false;
        SelectedCourseTitle.IsVisible = false;
        SelectedCourseDetailsPanel.IsVisible = false;
        SelectedDownloadStatusPanel.IsVisible = false;
        SelectedLevelTitle.Text = T("NoCourseSelected");
        SelectedCreatorRow.IsVisible = false;
        SelectedWorldRecordRow.IsVisible = false;
        SearchSelectedPreviewPanel.IsVisible = false;
    }

    private void ResetSavedSelectedLevelDetails()
    {
        _selectedPreviewLevel = null;
        SetSelectedSavedNode(null);
        SetSavedStatsHidden(false);
        CollapseSavedSelectedLevelContainer();
        SavedSelectedLevelContainer.IsVisible = false;
        RefreshSavedMetadataButton.IsVisible = false;
        OpenSelectedLevelFolderButton.IsVisible = false;
        RemoveSelectedPackButton.IsVisible = false;
        SavedLevelTitle.Text = T("SelectSavedCourse");
        SavedLevelDetailsPanel.IsVisible = false;
        CoursePreviewCanvas.Course = null;
    }

    private void SetSelectedSavedNode(SavedLevelNode? node)
    {
        if (_selectedSavedNode == node)
        {
            return;
        }

        if (_selectedSavedNode != null)
        {
            _selectedSavedNode.IsSelected = false;
        }

        _selectedSavedNode = node;
        if (_selectedSavedNode != null)
        {
            _selectedSavedNode.IsSelected = true;
        }
    }

    private void SetSavedStatsHidden(bool hidden)
    {
        foreach (var node in EnumerateSavedNodes(_savedNodes))
        {
            node.StatsHidden = hidden;
        }
    }

    private static IEnumerable<SavedLevelNode> EnumerateSavedNodes(IEnumerable<SavedLevelNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in EnumerateSavedNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private void ShowSearchSelectedLevelContainer()
    {
        SearchResultsLayout.ColumnDefinitions = new ColumnDefinitions("2*,*");
        SearchResultsLayout.ColumnSpacing = 12;
        SearchSelectedLevelContainer.IsVisible = true;
    }

    private void CollapseSearchSelectedLevelContainer()
    {
        SearchResultsLayout.ColumnDefinitions = new ColumnDefinitions("*,0");
        SearchResultsLayout.ColumnSpacing = 0;
    }

    private void ShowSavedSelectedLevelContainer()
    {
        SavedLevelsLayout.ColumnDefinitions = new ColumnDefinitions("360,*");
        SavedLevelsLayout.ColumnSpacing = 12;
        SavedSelectedLevelContainer.IsVisible = true;
    }

    private void CollapseSavedSelectedLevelContainer()
    {
        SavedLevelsLayout.ColumnDefinitions = new ColumnDefinitions("*,0");
        SavedLevelsLayout.ColumnSpacing = 0;
    }

    private void ShowProfileSelectedLevelContainer()
    {
        ProfileLevelsLayout.ColumnDefinitions = new ColumnDefinitions("2*,*");
        ProfileLevelsLayout.ColumnSpacing = 12;
        ProfileSelectedLevelContainer.IsVisible = true;
    }

    private void CollapseProfileSelectedLevelContainer()
    {
        ProfileLevelsLayout.ColumnDefinitions = new ColumnDefinitions("*,0");
        ProfileLevelsLayout.ColumnSpacing = 0;
    }

    private void UpdateProfileStats()
    {
        var levels = _profileLevels.ToList();
        var totalAttempts = levels.Sum(level => level.TotalAttempts);
        var totalClears = levels.Sum(level => level.Clears);
        var clearRate = totalAttempts > 0
            ? $"{(double)totalClears / totalAttempts * 100:0.##}%"
            : "n/a";

        ProfileTotalLevelsText.Text = $"Levels: {levels.Count}";
        ProfileTotalStarsText.Text = $"Stars: {levels.Sum(level => level.Stars)}";
        ProfileTotalAttemptsText.Text = $"Attempts: {totalAttempts}";
        ProfileTotalClearsText.Text = $"Clears: {totalClears}";
        ProfileAverageClearRateText.Text = $"Clear Rate: {clearRate}";
    }

    private void SetSelectedStats(LevelInfo level)
    {
        SelectedStarsText.Text = $"★ Stars: {level.StarsText}";
        SelectedTotalRunsText.Text = $"▶ Total Runs: {level.TotalAttemptsText}";
        SelectedClearsText.Text = $"✓ Clears: {level.ClearsText}";
        SelectedClearRateText.Text = $"% Clear Rate: {level.ClearRateText}";
    }

    private void SetProfileSelectedStats(LevelInfo level)
    {
        ProfileSelectedStarsText.Text = $"★ Stars: {level.StarsText}";
        ProfileSelectedTotalRunsText.Text = $"▶ Total Runs: {level.TotalAttemptsText}";
        ProfileSelectedClearsText.Text = $"✓ Clears: {level.ClearsText}";
        ProfileSelectedClearRateText.Text = $"% Clear Rate: {level.ClearRateText}";
    }

    private void SetSavedStats(LevelInfo level)
    {
        SavedStarsText.Text = $"★ Stars: {level.StarsText}";
        SavedTotalRunsText.Text = $"▶ Total Runs: {level.TotalAttemptsText}";
        SavedClearsText.Text = $"✓ Clears: {level.ClearsText}";
    }

    private async void DownloadLevelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is LevelInfo level)
        {
            await DownloadLevelAsync(level);
        }
    }

    private async void DownloadAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var level in _searchResults.ToList())
        {
            if (CanStartDownload(level))
            {
                await DownloadLevelAsync(level);
            }
        }
    }

    private void SearchResultsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is not LevelInfo level)
        {
            ResetSearchSelectedLevelDetails();
            return;
        }

        ShowSearchSelectedLevelContainer();
        SelectedCourseTitle.IsVisible = true;
        SelectedCourseDetailsPanel.IsVisible = true;
        SelectedDownloadStatusPanel.IsVisible = true;
        SelectedLevelTitle.Text = $"{level.Name} | {level.DisplayCode}";
        SelectedCreatorNameText.Text = string.IsNullOrWhiteSpace(level.Creator) ? "n/a" : level.Creator;
        SelectedWorldRecordText.Text = level.WorldRecordMs > 0
            ? $"{FormatTime(level.WorldRecordMs)} by {FormatName(level.WorldRecordHolderNnid)}"
            : "n/a";
        SelectedUploadText.Text = $"Upload: {FormatUploadTime(level.UploadTime)}";
        SetSelectedStats(level);
        ShowInlineCoursePreview(level, SearchSelectedPreviewCanvas, SearchSelectedPreviewPanel);
        ShowSelectedSearchMiiImages(level);
        _ = LoadSearchResultMiiImagesAsync(level);
    }

    private void SavedLevelsTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SavedLevelsTreeView.SelectedItem is SavedLevelNode { Level: { } level } node)
        {
            SetSavedStatsHidden(true);
            SetSelectedSavedNode(node);
            _selectedPackNode = null;
            _selectedPreviewLevel = level;
            _selectedPreviewFile = "course_data.cdt";
            SetStatus($"{level.Name} selected.");
            RefreshSavedMetadataButton.IsVisible = CanRefreshSavedMetadata();
            OpenSelectedLevelFolderButton.IsVisible = true;
            RemoveSelectedPackButton.IsVisible = false;
            ShowSavedSelectedLevelContainer();
            SavedLevelDetailsPanel.IsVisible = true;
            ShowSavedLevelDetails(level);
            _ = LoadSavedLevelMiiImagesAsync(level);
            LoadCoursePreview(level, _selectedPreviewFile);
            return;
        }

        ResetSavedSelectedLevelDetails();
    }

    private void SavedLevelNode_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<Button>() != null)
        {
            return;
        }

        if ((sender as Control)?.DataContext is not SavedLevelNode node)
        {
            return;
        }

        if (node is { IsFolder: true })
        {
            SavedLevelsTreeView.SelectedItem = null;
            ResetSavedSelectedLevelDetails();
            _selectedPackNode = node;
            RemoveSelectedPackButton.IsVisible = false;

            var treeViewItem = (sender as Control)?.FindAncestorOfType<TreeViewItem>();
            if (treeViewItem != null)
            {
                treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
            }

            e.Handled = true;
            return;
        }

        if (node.Level != null && IsSameSavedLevel(node.Level, _selectedPreviewLevel))
        {
            SavedLevelsTreeView.SelectedItem = null;
            ResetSavedSelectedLevelDetails();
            e.Handled = true;
        }
    }

    private void OverworldPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPreviewLevel == null)
        {
            return;
        }

        _selectedPreviewFile = "course_data.cdt";
        LoadCoursePreview(_selectedPreviewLevel, _selectedPreviewFile);
    }

    private void UnderworldPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPreviewLevel == null)
        {
            return;
        }

        _selectedPreviewFile = "course_data_sub.cdt";
        LoadCoursePreview(_selectedPreviewLevel, _selectedPreviewFile);
    }

    private void HiddenBlocksPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CoursePreviewCanvas.ShowHiddenBlocks = !CoursePreviewCanvas.ShowHiddenBlocks;
        HiddenBlocksPreviewButton.Content = CoursePreviewCanvas.ShowHiddenBlocks
            ? T("HideHiddenBlocks")
            : T("RevealHiddenBlocks");
    }

    private void RefreshSavedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LoadSavedLevels();
    }

    private void SavedSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySavedFilter();
    }

    private void SavedSourceComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        LoadSavedLevels();
    }

    private void OpenSavedFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopIntegration.OpenPath(GetSelectedSavedRoot());
    }

    private void OpenSelectedLevelFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPreviewLevel == null)
        {
            return;
        }

        DesktopIntegration.OpenPath(ResolveCourseFolder(_selectedPreviewLevel));
    }

    private async void DeleteSavedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPackNode is { PackName: { } packName, PackFolder: { } packFolder })
        {
            await RemovePackFolderAsync(packName, packFolder);
            return;
        }

        if (SavedLevelsTreeView.SelectedItem is not SavedLevelNode { Level: { } level })
        {
            return;
        }

        await RunSafeAsync(token =>
        {
            _downloadService.Delete(level);
            LoadSavedLevels();
            SetStatus($"Deleted {level.LevelId}.");
            return Task.CompletedTask;
        });
    }

    private async void RemoveSelectedPackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPackNode is not { PackName: { } packName, PackFolder: { } packFolder })
        {
            return;
        }

        await RemovePackFolderAsync(packName, packFolder);
    }

    private async void DeletePackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not SavedLevelNode { PackName: { } packName, PackFolder: { } packFolder })
        {
            return;
        }

        e.Handled = true;
        var confirmed = await ShowConfirmDialogAsync(
            T("RemovePackTitle"),
            string.Format(CultureInfo.InvariantCulture, T("RemovePackWarning"), packName),
            T("Delete"),
            T("Cancel"));
        if (!confirmed)
        {
            return;
        }

        await RemovePackFolderAsync(packName, packFolder);
    }

    private async Task RemovePackFolderAsync(string packName, string packFolder)
    {
        await RunSafeAsync(_ =>
        {
            _downloadService.RemovePackFolder(packName, packFolder);
            _selectedPackNode = null;
            RefreshPackNameSuggestions();
            LoadSavedLevels();
            SetStatus($"Removed level pack '{packName}' and moved its courses back.");
            return Task.CompletedTask;
        });
    }

    private async void ResetOfficialButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync(async token =>
        {
            SetBusy("Resetting official courses...");
            await _downloadService.ResetOfficialCoursesAsync(new Progress<string>(SetStatus), token);
            LoadSavedLevels();
            SetStatus("Official courses reset.");
        });
    }

    private async void RefreshSavedMetadataButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPreviewLevel == null)
        {
            return;
        }

        await RunSafeAsync(async token =>
        {
            SaveSettingsFromUi();
            var oldLevel = _selectedPreviewLevel;
            SetBusy($"Refreshing metadata for {oldLevel.LevelId}...");
            var fresh = await _apiClient.GetLevelAsync(_settings, oldLevel.LevelId, token);
            if (fresh == null)
            {
                SetStatus($"No API metadata found for {oldLevel.LevelId}.");
                return;
            }

            fresh.Folder = oldLevel.Folder;
            fresh.Pack = oldLevel.Pack;
            SaveRefreshedSavedLevel(oldLevel, fresh);
            LoadSavedLevels();
            _selectedPreviewLevel = fresh;
            ShowSavedLevelDetails(fresh);
            _ = LoadSavedLevelMiiImagesAsync(fresh);
            LoadCoursePreview(fresh, _selectedPreviewFile);
            SetStatus($"Refreshed metadata for {fresh.LevelId}.");
        });
    }

    private async void RefreshAllDownloadedDataButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync(async token =>
        {
            SaveSettingsFromUi();
            var downloaded = _store.LoadDownloaded();
            var entries = downloaded.ToList();
            if (entries.Count == 0)
            {
                SetStatus("No downloaded courses to refresh.");
                return;
            }

            RefreshAllDownloadedDataButton.IsEnabled = false;
            try
            {
                var refreshed = 0;
                var failed = 0;
                for (var i = 0; i < entries.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var (key, oldLevel) = entries[i];
                    SetStatus($"Refreshing {i + 1}/{entries.Count}: {oldLevel.LevelId}...");

                    var fresh = await FetchLevelWithQueueRetryAsync(oldLevel.LevelId, token);
                    if (fresh == null)
                    {
                        failed++;
                        continue;
                    }

                    fresh.Folder = oldLevel.Folder;
                    fresh.Pack = oldLevel.Pack;
                    downloaded[key] = fresh;
                    _store.SaveDownloaded(downloaded);
                    refreshed++;

                    if (i + 1 < entries.Count)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(900), token);
                    }
                }

                LoadSavedLevels();
                SetStatus($"Downloaded metadata refresh complete: {refreshed} refreshed, {failed} failed.");
            }
            finally
            {
                RefreshAllDownloadedDataButton.IsEnabled = true;
            }
        });
    }

    private async Task<LevelInfo?> FetchLevelWithQueueRetryAsync(long levelId, CancellationToken token)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await _apiClient.GetLevelAsync(_settings, levelId, token);
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                SetStatus($"API throttled or failed for {levelId}; retrying...");
                await Task.Delay(TimeSpan.FromSeconds(4 * attempt), token);
            }
        }

        SetStatus($"Could not refresh {levelId}.");
        return null;
    }

    private async void SelectCemuFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = await DesktopIntegration.PickFolderAsync(this, "Select Cemu Folder");
        if (path == null)
        {
            return;
        }

        CemuPathTextBox.Text = path;
        LoadProfiles(path);
    }

    private void SaveSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        _store.SaveSettings(_settings);
        ApplyLanguage();
        SetStatus(T("SettingsSaved"));
    }

    private void ResetSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _settings = new AppSettings();
        LoadSettingsIntoUi();
        _store.SaveSettings(_settings);
        ApplyLanguage();
        SetStatus(T("SettingsReset"));
    }

    private void LanguageComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        SaveSettingsFromUi();
        ApplyLanguage();
        _store.SaveSettings(_settings);
    }

    private void HideViewerInfoCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        _settings.HideViewerInfo = HideViewerInfoCheckBox.IsChecked == true;
        ApplyViewerInfoVisibility();
        _store.SaveSettings(_settings);
    }

    private void OpenProxyFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!File.Exists(_paths.ProxyFile))
        {
            File.WriteAllText(_paths.ProxyFile, "");
        }

        DesktopIntegration.OpenPath(_paths.ProxyFile);
    }

    private void UpdateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_availableUpdateUrl))
        {
            DesktopIntegration.OpenPath(_availableUpdateUrl);
        }
    }

    private void RefreshPackNameSuggestions()
    {
        PackNameTextBox.ItemsSource = _store.LoadLevelPacks()
            .Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void PackNameTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isApplyingPackNameSuggestion)
        {
            return;
        }

        var text = PackNameTextBox.Text?.Trim() ?? "";
        var hasMatch = !string.IsNullOrWhiteSpace(text) &&
                       _store.LoadLevelPacks().Keys.Any(name =>
                           name.Contains(text, StringComparison.OrdinalIgnoreCase));
        PackNameTextBox.IsDropDownOpen = hasMatch;
    }

    private async void PackNameTextBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var packName = e.AddedItems.OfType<string>().FirstOrDefault() ??
                       PackNameTextBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(packName))
        {
            return;
        }

        _isApplyingPackNameSuggestion = true;
        try
        {
            ApplyPackNameSuggestion(packName);
            await Dispatcher.UIThread.InvokeAsync(() => ApplyPackNameSuggestion(packName), DispatcherPriority.Loaded);
            await Task.Delay(50);
            ApplyPackNameSuggestion(packName);
        }
        finally
        {
            _isApplyingPackNameSuggestion = false;
        }
    }

    private void ApplyPackNameSuggestion(string packName)
    {
        PackNameTextBox.Text = packName;
        PackNameTextBox.IsDropDownOpen = false;
        PackNameTextBox.SelectedItem = null;
    }

    private async Task DownloadLevelAsync(LevelInfo level)
    {
        if (IsLevelDownloaded(level))
        {
            MarkDownloadState(level);
            return;
        }

        var state = GetDownloadState(level.LevelId);
        if (state.IsDownloading)
        {
            ApplyDownloadState(level);
            return;
        }

        using var cts = new CancellationTokenSource();
        string? packFolder = null;
        try
        {
            SaveSettingsFromUi();
            DownloadProgressBar.Value = 0;
            state.IsDownloaded = false;
            state.IsDownloading = true;
            state.ProgressText = "Starting download...";
            ApplyDownloadStateToSearchResults(level.LevelId);
            var progress = new Progress<string>(message =>
            {
                SetStatus(message);
                DownloadStatusText.Text = message;
                DownloadProgressBar.Value = Math.Min(100, DownloadProgressBar.Value + 18);
                state.ProgressText = $"{DownloadProgressBar.Value:0}% - {message}";
                ApplyDownloadStateToSearchResults(level.LevelId);
            });

            packFolder = GetPackFolderIfEnabled();
            await _downloadService.DownloadAsync(level, packFolder, progress, cts.Token);
            DownloadProgressBar.Value = 100;
            state.ProgressText = "100% - Download complete.";
            state.IsDownloaded = true;
            LoadDownloadedThumbnail(level);
            LoadSavedLevels();
        }
        catch (Exception ex)
        {
            _downloadService.CleanupFailedDownload(level, packFolder);
            state.IsDownloaded = false;
            state.IsDownloading = false;
            state.ProgressText = "";
            level.IsDownloaded = false;
            level.IsDownloading = false;
            level.DownloadProgressText = "";
            level.Folder = "";
            ApplyDownloadStateToSearchResults(level.LevelId);
            LoadSavedLevels();
            state.ProgressText = ex.Message;
            SetStatus(ex.Message);
            DownloadStatusText.Text = ex.Message;
        }
        finally
        {
            state.IsDownloading = false;
            ApplyDownloadStateToSearchResults(level.LevelId);
        }
    }

    private void LoadSettingsIntoUi()
    {
        _settings = _store.LoadSettings();
        SearchTextBox.Text = _settings.LastSearchPhrase;
        SearchLevelNameCheckBox.IsChecked = _settings.SearchParams.LevelName;
        SearchLevelIdCheckBox.IsChecked = _settings.SearchParams.LevelID;
        SearchCreatorNameCheckBox.IsChecked = _settings.SearchParams.CreatorName;
        SearchCreatorIdCheckBox.IsChecked = _settings.SearchParams.CreatorID;
        SearchExactCheckBox.IsChecked = _settings.SearchParams.SearchExact;
        UseCemuCheckBox.IsChecked = _settings.UseCemuDir;
        CemuPathTextBox.Text = _settings.CemuDirPath;
        HideViewerInfoCheckBox.IsChecked = _settings.HideViewerInfo;
        UseProxyCheckBox.IsChecked = _settings.UseProxy;
        ApiLinkTextBox.Text = _settings.ApiLink;
        SelectComboBoxItemByTag(LanguageComboBox, _settings.Language);
        LoadProfiles(_settings.CemuDirPath);
    }

    private void SaveSettingsFromUi()
    {
        _settings.LastSearchPhrase = SearchTextBox.Text?.Trim() ?? "";
        _settings.SearchParams.LevelName = SearchLevelNameCheckBox.IsChecked == true;
        _settings.SearchParams.LevelID = SearchLevelIdCheckBox.IsChecked == true;
        _settings.SearchParams.CreatorName = SearchCreatorNameCheckBox.IsChecked == true;
        _settings.SearchParams.CreatorID = SearchCreatorIdCheckBox.IsChecked == true;
        _settings.SearchParams.SearchExact = SearchExactCheckBox.IsChecked == true;
        _settings.UseCemuDir = UseCemuCheckBox.IsChecked == true;
        _settings.CemuDirPath = CemuPathTextBox.Text?.Trim() ?? "";
        _settings.HideViewerInfo = HideViewerInfoCheckBox.IsChecked == true;
        _settings.UseProxy = UseProxyCheckBox.IsChecked == true;
        _settings.ApiLink = string.IsNullOrWhiteSpace(ApiLinkTextBox.Text)
            ? "https://api.bobac-analytics.com/smm1"
            : ApiLinkTextBox.Text.Trim();
        _settings.SelectedProfile = ProfileComboBox.SelectedItem?.ToString() ?? _settings.SelectedProfile;
        _settings.Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
    }

    private void ApplyLanguage()
    {
        DownloadTab.Header = T("DownloadTab");
        SavedCoursesTab.Header = T("SavedCoursesTab");
        SettingsTab.Header = T("SettingsTab");

        SearchCoursesTitle.Text = T("SearchCourses");
        SearchTextBox.Watermark = T("SearchWatermark");
        SearchButton.Content = T("Search");
        RandomButton.Content = T("Random");
        SearchLevelNameCheckBox.Content = T("LevelName");
        SearchLevelIdCheckBox.Content = T("LevelId");
        SearchCreatorNameCheckBox.Content = T("CreatorName");
        SearchCreatorIdCheckBox.Content = T("CreatorId");
        SearchExactCheckBox.Content = T("Exact");
        DownloadToPackCheckBox.Content = T("SaveInLevelPack");
        PackNameTextBox.Watermark = T("LevelPackName");
        DownloadAllButton.Content = T("DownloadAllResults");
        SelectedCourseTitle.Text = T("SelectedCourse");
        SelectedLevelTitle.Text = T("NoCourseSelected");
        SelectedCreatorHeader.Text = T("Creator");
        SelectedWorldRecordHeader.Text = T("WorldRecord");
        BackFromProfileButton.Content = T("Back");
        BackFromViewerButton.Content = T("Back");
        ProfileTitle.Text = T("UserProfile");
        ProfileSelectedLevelTitle.Text = T("NoLevelSelected");
        ProfileSelectedCreatorHeader.Text = T("Creator");
        ProfileSelectedWorldRecordHeader.Text = T("WorldRecord");

        foreach (var item in SavedSourceComboBox.Items.OfType<ComboBoxItem>())
        {
            item.Content = item.Tag?.ToString() switch
            {
                "downloaded" => T("SavedCourses"),
                "official" => T("OfficialTestingCourses"),
                "cemu" => T("CemuProfileCourses"),
                "backupped" => T("BackuppedCourses"),
                _ => item.Content
            };
        }

        SavedSearchTextBox.Watermark = T("FilterSavedCourses");
        RefreshSavedButton.Content = T("Refresh");
        ToolTip.SetTip(RefreshSavedMetadataButton, T("RefreshData"));
        ToolTip.SetTip(OpenSelectedLevelFolderButton, T("OpenLevelFolder"));
        OpenSavedFolderButton.Content = T("OpenFolder");
        DeleteSavedButton.Content = T("DeleteSelected");
        RemoveSelectedPackButton.Content = T("RemoveCoursePackFolder");
        ResetOfficialButton.Content = T("ResetOfficialCourses");
        SavedLevelTitle.Text = T("SelectSavedCourse");
        CoursePreviewInfo.Text = T("CoursePreviewHint");
        OverworldPreviewButton.Content = T("Overworld");
        UnderworldPreviewButton.Content = T("Underworld");
        HiddenBlocksPreviewButton.Content = CoursePreviewCanvas.ShowHiddenBlocks
            ? T("HideHiddenBlocks")
            : T("RevealHiddenBlocks");
        LevelViewerOverworldButton.Content = T("Overworld");
        LevelViewerUnderworldButton.Content = T("Underworld");
        LevelViewerHiddenBlocksButton.Content = LevelViewerCanvas.ShowHiddenBlocks
            ? T("HideHiddenBlocks")
            : T("RevealHiddenBlocks");
        SavedCreatorHeader.Text = T("Creator");
        SavedClearRateHeader.Text = T("ClearRate");
        SavedWorldRecordHeader.Text = T("WorldRecord");

        SettingsTitle.Text = T("Settings");
        LanguageLabel.Text = T("Language");
        UseCemuCheckBox.Content = T("UseCemuFolder");
        CemuPathTextBox.Watermark = T("CemuFolderPath");
        SelectCemuFolderButton.Content = T("SelectFolder");
        HideViewerInfoCheckBox.Content = T("HideViewerInfo");
        UseProxyCheckBox.Content = T("UseProxy");
        OpenProxyFileButton.Content = T("OpenProxyFile");
        ApiEndpointLabel.Text = T("ApiEndpoint");
        SaveSettingsButton.Content = T("SaveSettings");
        ResetSettingsButton.Content = T("ResetSettings");
        RefreshAllDownloadedDataButton.Content = T("RefreshAllDownloadedData");
        UpdateApiStatusUi();
        UpdateReleaseStatusUi();
        ApplyViewerInfoVisibility();
    }

    private string T(string key)
    {
        var de = string.Equals(_settings.Language, "de", StringComparison.OrdinalIgnoreCase);
        return de ? GermanText.GetValueOrDefault(key, EnglishText[key]) : EnglishText[key];
    }

    private static void SelectComboBoxItemByTag(ComboBox comboBox, string? tag)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) ??
            comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private void LoadProfiles(string? cemuPath)
    {
        ProfileComboBox.Items.Clear();
        if (string.IsNullOrWhiteSpace(cemuPath))
        {
            return;
        }

        var userRoot = Path.Combine(cemuPath, "mlc01", "usr", "save", "00050000", "1018dd00", "user");
        if (!Directory.Exists(userRoot))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(userRoot)
                     .Select(Path.GetFileName)
                     .Where(name => !string.IsNullOrWhiteSpace(name) && name != "common"))
        {
            ProfileComboBox.Items.Add(directory);
            if (directory == _settings.SelectedProfile)
            {
                ProfileComboBox.SelectedItem = directory;
            }
        }
    }

    private void LoadSavedLevels()
    {
        var source = GetSavedSource();
        _allSavedLevels = source switch
        {
            "official" => LoadLevelsFromFolders(Path.Combine(_paths.OfficialCoursesDirectory, "CourseFiles")),
            "cemu" => LoadLevelsFromFolders(GetCemuProfilePath()),
            "backupped" => JsonStore.ToLevelList(LoadLevelFile(_paths.BackuppedFile)),
            _ => JsonStore.ToLevelList(_store.LoadDownloaded())
        };
        _allSavedNodes = source == "downloaded"
            ? LoadDownloadedSavedCourseNodes()
            : _allSavedLevels.Select(ToLevelNode).ToList();

        ResetSavedSelectedLevelDetails();
        ApplySavedFilter();
        SetStatus($"Loaded {_allSavedLevels.Count} saved course(s).");
    }

    private void AddSearchResult(LevelInfo level)
    {
        MarkDownloadState(level);
        _searchResults.Add(level);
        _ = LoadSearchResultMiiImagesAsync(level);
    }

    private async Task LoadSearchResultMiiImagesAsync(LevelInfo level)
    {
        level.CreatorMiiImage = await _miiImageLoader.LoadAsync(level.CreatorMiiData, CancellationToken.None);
        if (SearchResultsListBox.SelectedItem == level)
        {
            ShowSelectedSearchMiiImages(level);
        }

        level.WorldRecordMiiImage = await _miiImageLoader.LoadAsync(level.WorldRecordBestTimePlayerMiiData, CancellationToken.None);
        if (SearchResultsListBox.SelectedItem == level)
        {
            ShowSelectedSearchMiiImages(level);
        }
    }

    private void ShowSelectedSearchMiiImages(LevelInfo level)
    {
        SelectedCreatorMiiImage.Source = level.CreatorMiiImage;
        SelectedCreatorMiiPanel.IsVisible = level.CreatorMiiImage != null;
        SelectedCreatorRow.IsVisible = level.CreatorMiiImage != null;
        SelectedWorldRecordMiiImage.Source = level.WorldRecordMiiImage;
        SelectedWorldRecordMiiPanel.IsVisible = level.WorldRecordMiiImage != null;
        SelectedWorldRecordRow.IsVisible = level.WorldRecordMiiImage != null;
    }

    private void ShowSelectedProfileMiiImages(LevelInfo level)
    {
        ProfileSelectedCreatorMiiImage.Source = level.CreatorMiiImage;
        ProfileSelectedCreatorRow.IsVisible = level.CreatorMiiImage != null;
        ProfileSelectedWorldRecordMiiImage.Source = level.WorldRecordMiiImage;
        ProfileSelectedWorldRecordRow.IsVisible = level.WorldRecordMiiImage != null;
    }

    private void AddProfileLevel(LevelInfo level)
    {
        MarkDownloadState(level);
        _profileLevels.Add(level);
        _ = LoadProfileLevelMiiImagesAsync(level);
    }

    private async Task LoadProfileLevelMiiImagesAsync(LevelInfo level)
    {
        level.CreatorMiiImage = await _miiImageLoader.LoadAsync(level.CreatorMiiData, CancellationToken.None);
        if (string.Equals(level.Creator, _currentProfileUserName, StringComparison.OrdinalIgnoreCase) ||
            ProfileHeaderMiiImage.Source == null)
        {
            ProfileHeaderMiiImage.Source = level.CreatorMiiImage;
        }

        level.WorldRecordMiiImage = await _miiImageLoader.LoadAsync(level.WorldRecordBestTimePlayerMiiData, CancellationToken.None);
        if (ProfileLevelsListBox.SelectedItem == level)
        {
            ShowSelectedProfileMiiImages(level);
        }
    }

    private void MarkDownloadState(LevelInfo level)
    {
        var state = GetDownloadState(level.LevelId);
        if (IsLevelDownloaded(level))
        {
            state.IsDownloaded = true;
            state.IsDownloading = false;
            state.ProgressText = "";
        }

        ApplyDownloadState(level);
        if (level.IsDownloaded)
        {
            LoadDownloadedThumbnail(level);
        }
    }

    private void LoadDownloadedThumbnail(LevelInfo level)
    {
        if (level.Thumbnail != null)
        {
            return;
        }

        var folder = ResolveDownloadedCourseFolder(level);
        if (folder == null)
        {
            return;
        }

        level.Folder = folder;
        level.Thumbnail = _thumbnailLoader.LoadCourseThumbnail(folder);
    }

    private bool CanStartDownload(LevelInfo level)
    {
        var state = GetDownloadState(level.LevelId);
        if (state.IsDownloading)
        {
            ApplyDownloadState(level);
            return false;
        }

        if (state.IsDownloaded || IsLevelDownloaded(level))
        {
            state.IsDownloaded = true;
            ApplyDownloadState(level);
            LoadDownloadedThumbnail(level);
            return false;
        }

        return true;
    }

    private DownloadState GetDownloadState(long levelId)
    {
        if (!_downloadStates.TryGetValue(levelId, out var state))
        {
            state = new DownloadState();
            _downloadStates[levelId] = state;
        }

        return state;
    }

    private void ApplyDownloadState(LevelInfo level)
    {
        var state = GetDownloadState(level.LevelId);
        level.IsDownloading = state.IsDownloading;
        level.IsDownloaded = state.IsDownloaded;
        level.DownloadProgressText = state.ProgressText;
    }

    private void ApplyDownloadStateToSearchResults(long levelId)
    {
        foreach (var result in _searchResults.Where(result => result.LevelId == levelId))
        {
            ApplyDownloadState(result);
        }

        foreach (var result in _profileLevels.Where(result => result.LevelId == levelId))
        {
            ApplyDownloadState(result);
        }
    }

    private bool IsLevelDownloaded(LevelInfo level)
    {
        if (Directory.Exists(Path.Combine(_paths.DownloadCacheDirectory, level.LevelId.ToString())))
        {
            return true;
        }

        var packs = _store.LoadLevelPacks();
        return packs.Values.Any(packFolder =>
        {
            var packRoot = Path.Combine(_paths.LevelPacksDirectory, packFolder);
            return Directory.Exists(packRoot) &&
                   Directory.EnumerateDirectories(packRoot).Any(directory => TryReadLevelIdFromFolder(directory) == level.LevelId);
        });
    }

    private Dictionary<string, LevelInfo> LoadLevelFile(string path)
    {
        return File.Exists(path)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, LevelInfo>>(File.ReadAllText(path), JsonStore.JsonOptions) ?? []
            : [];
    }

    private void SaveRefreshedSavedLevel(LevelInfo oldLevel, LevelInfo freshLevel)
    {
        var source = GetSavedSource();
        if (source == "backupped")
        {
            var levels = LoadLevelFile(_paths.BackuppedFile);
            levels[FindLevelKey(levels, oldLevel)] = freshLevel;
            File.WriteAllText(_paths.BackuppedFile, JsonSerializer.Serialize(levels, JsonStore.JsonOptions));
            return;
        }

        var downloaded = _store.LoadDownloaded();
        var key = FindLevelKey(downloaded, oldLevel);
        if (source == "packs" && !string.IsNullOrWhiteSpace(freshLevel.Pack))
        {
            key = string.IsNullOrWhiteSpace(key)
                ? $"{freshLevel.LevelId}_{freshLevel.Pack}"
                : key;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            key = freshLevel.LevelId.ToString();
        }

        downloaded[key] = freshLevel;
        _store.SaveDownloaded(downloaded);
    }

    private bool CanRefreshSavedMetadata()
    {
        return GetSavedSource() is "downloaded" or "backupped";
    }

    private static string FindLevelKey(Dictionary<string, LevelInfo> levels, LevelInfo level)
    {
        var folder = string.IsNullOrWhiteSpace(level.Folder) ? null : Path.GetFullPath(level.Folder);
        var match = levels.FirstOrDefault(item =>
            item.Value.LevelId == level.LevelId &&
            (string.IsNullOrWhiteSpace(level.Pack) ||
             string.Equals(item.Value.Pack, level.Pack, StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrWhiteSpace(match.Key))
        {
            return match.Key;
        }

        if (folder != null)
        {
            match = levels.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.Value.Folder) &&
                string.Equals(Path.GetFullPath(item.Value.Folder), folder, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match.Key))
            {
                return match.Key;
            }
        }

        return level.LevelId.ToString();
    }

    private IReadOnlyList<LevelInfo> LoadLevelsFromFolders(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateDirectories(root)
            .Select(directory => new LevelInfo
            {
                LevelId = long.TryParse(Path.GetFileName(directory), out var id) ? id : 0,
                Name = Path.GetFileName(directory) ?? directory,
                Creator = "Local",
                Folder = directory
            })
            .ToList();
    }

    private IReadOnlyList<SavedLevelNode> LoadLevelsFromLevelPacks()
    {
        var packs = _store.LoadLevelPacks();
        var downloadedLevels = _store.LoadDownloaded();
        var downloadedByFolder = downloadedLevels
            .Values
            .Where(level => !string.IsNullOrWhiteSpace(level.Folder))
            .ToDictionary(level => Path.GetFullPath(level.Folder), StringComparer.OrdinalIgnoreCase);
        var nodes = new List<SavedLevelNode>();
        foreach (var (packName, packFolder) in packs)
        {
            var root = Path.Combine(_paths.LevelPacksDirectory, packFolder);
            var children = new ObservableCollection<SavedLevelNode>();
            if (!Directory.Exists(root))
            {
                nodes.Add(new SavedLevelNode
                {
                    DisplayName = packName,
                    Summary = "0 courses",
                    ShortInfo = "0 courses",
                    IsFolder = true,
                    RowMargin = new Thickness(-18, 5, 0, 5),
                    PackName = packName,
                    PackFolder = packFolder,
                    Children = children
                });
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName))
            {
                var fullPath = Path.GetFullPath(directory);
                var levelId = TryReadLevelIdFromFolder(directory);
                if (!downloadedByFolder.TryGetValue(fullPath, out var level))
                {
                    level = FindDownloadedPackLevel(downloadedLevels, levelId, packName) ?? new LevelInfo
                    {
                        LevelId = levelId,
                        Name = GetDisplayNameFromCourseFolder(directory),
                        Creator = "Local",
                        Pack = packName,
                        Folder = directory
                    };
                }

                level.Pack = packName;
                level.Folder = directory;
                children.Add(ToLevelNode(level));
            }

            nodes.Add(new SavedLevelNode
            {
                DisplayName = packName,
                Summary = $"{children.Count} course(s)",
                ShortInfo = $"{children.Count} course(s)",
                IsFolder = true,
                RowMargin = new Thickness(-18, 5, 0, 5),
                PackName = packName,
                PackFolder = packFolder,
                Children = children
            });
        }

        return nodes.OrderBy(node => node.DisplayName).ToList();
    }

    private static LevelInfo? FindDownloadedPackLevel(
        Dictionary<string, LevelInfo> downloadedLevels,
        long levelId,
        string packName)
    {
        if (downloadedLevels.TryGetValue($"{levelId}_{packName}", out var exact))
        {
            return exact;
        }

        return downloadedLevels.Values.FirstOrDefault(level =>
            level.LevelId == levelId &&
            string.Equals(level.Pack, packName, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<SavedLevelNode> LoadDownloadedSavedCourseNodes()
    {
        var standaloneLevels = JsonStore.ToLevelList(_store.LoadDownloaded())
            .Where(level => string.IsNullOrWhiteSpace(level.Pack))
            .Select(ToLevelNode);

        return standaloneLevels
            .Concat(LoadLevelsFromLevelPacks())
            .OrderBy(node => node.IsFolder ? 0 : 1)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadCoursePreview(LevelInfo level, string courseFileName)
    {
        try
        {
            var folder = ResolveCourseFolder(level);
            var courseData = Path.Combine(folder, courseFileName);
            if (!File.Exists(courseData))
            {
                CoursePreviewCanvas.Course = null;
                CoursePreviewInfo.Text = $"No {courseFileName} found for this entry.";
                return;
            }

            var preview = _courseParser.Read(courseData);
            CoursePreviewCanvas.Course = preview;
            var areaName = courseFileName == "course_data_sub.cdt" ? "Underworld" : "Overworld";
            CoursePreviewInfo.Text = $"{areaName} - {preview.Summary}";
            ScrollCoursePreviewToBottomLeft();
        }
        catch (Exception ex)
        {
            CoursePreviewCanvas.Course = null;
            CoursePreviewInfo.Text = $"Preview could not be loaded: {ex.Message}";
        }
    }

    private void ScrollCoursePreviewToBottomLeft()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var maxY = Math.Max(0, CoursePreviewScrollViewer.Extent.Height - CoursePreviewScrollViewer.Viewport.Height);
            CoursePreviewScrollViewer.Offset = new Vector(0, maxY);
        }, DispatcherPriority.Background);
    }

    private string ResolveCourseFolder(LevelInfo level)
    {
        if (!string.IsNullOrWhiteSpace(level.Folder) && Directory.Exists(level.Folder))
        {
            return level.Folder;
        }

        var packs = _store.LoadLevelPacks();
        if (!string.IsNullOrWhiteSpace(level.Pack))
        {
            var packFolder = packs.GetValueOrDefault(level.Pack, level.Pack);
            var packRoot = Path.Combine(_paths.LevelPacksDirectory, packFolder);
            var matchingFolder = Directory.Exists(packRoot)
                ? Directory.EnumerateDirectories(packRoot)
                    .FirstOrDefault(directory => TryReadLevelIdFromFolder(directory) == level.LevelId)
                : null;
            return matchingFolder ?? Path.Combine(packRoot, level.LevelId.ToString());
        }

        return Path.Combine(_paths.DownloadCacheDirectory, level.LevelId.ToString());
    }

    private string? ResolveDownloadedCourseFolder(LevelInfo level)
    {
        if (!string.IsNullOrWhiteSpace(level.Folder) && Directory.Exists(level.Folder))
        {
            return level.Folder;
        }

        var cacheFolder = Path.Combine(_paths.DownloadCacheDirectory, level.LevelId.ToString());
        if (Directory.Exists(cacheFolder))
        {
            return cacheFolder;
        }

        var downloadedMatch = _store.LoadDownloaded()
            .Values
            .FirstOrDefault(saved => saved.LevelId == level.LevelId &&
                                     !string.IsNullOrWhiteSpace(saved.Folder) &&
                                     Directory.Exists(saved.Folder));
        if (downloadedMatch != null)
        {
            return downloadedMatch.Folder;
        }

        var packs = _store.LoadLevelPacks();
        foreach (var packFolder in packs.Values)
        {
            var packRoot = Path.Combine(_paths.LevelPacksDirectory, packFolder);
            if (!Directory.Exists(packRoot))
            {
                continue;
            }

            var matchingFolder = Directory.EnumerateDirectories(packRoot)
                .FirstOrDefault(directory => TryReadLevelIdFromFolder(directory) == level.LevelId);
            if (matchingFolder != null)
            {
                return matchingFolder;
            }
        }

        return null;
    }

    private void ShowInlineCoursePreview(LevelInfo level, CoursePreviewControl previewControl, Control panel)
    {
        var folder = ResolveDownloadedCourseFolder(level);
        if (folder == null)
        {
            panel.IsVisible = false;
            return;
        }

        var courseData = Path.Combine(folder, "course_data.cdt");
        if (!File.Exists(courseData))
        {
            panel.IsVisible = false;
            return;
        }

        try
        {
            previewControl.Course = _courseParser.Read(courseData);
            panel.IsVisible = true;
        }
        catch (Exception ex)
        {
            previewControl.Course = null;
            panel.IsVisible = false;
            SetStatus($"Preview could not be loaded: {ex.Message}");
        }
    }

    private void OpenLargeLevelViewer(LevelInfo level, bool returnToProfilePage)
    {
        var folder = ResolveDownloadedCourseFolder(level);
        if (folder == null)
        {
            SetStatus("Download the level first to open the viewer.");
            return;
        }

        _largeViewerLevel = level;
        _largeViewerFile = "course_data.cdt";
        _returnToProfilePageFromViewer = returnToProfilePage;
        MainTabs.IsVisible = false;
        ProfilePage.IsVisible = false;
        LevelViewerPage.IsVisible = true;
        LevelViewerTitle.Text = $"{level.Name} | {level.DisplayCode}";
        LevelViewerCanvas.ShowHiddenBlocks = false;
        LevelViewerHiddenBlocksButton.Content = T("RevealHiddenBlocks");
        LoadLargeLevelViewer(level, _largeViewerFile);
    }

    private void LoadLargeLevelViewer(LevelInfo level, string courseFileName)
    {
        var folder = ResolveDownloadedCourseFolder(level);
        var courseData = folder == null ? "" : Path.Combine(folder, courseFileName);
        if (string.IsNullOrWhiteSpace(courseData) || !File.Exists(courseData))
        {
            LevelViewerCanvas.Course = null;
            LevelViewerInfo.Text = $"No {courseFileName} found for this entry.";
            return;
        }

        try
        {
            var preview = _courseParser.Read(courseData);
            LevelViewerCanvas.Course = preview;
            var areaName = courseFileName == "course_data_sub.cdt" ? T("Underworld") : T("Overworld");
            LevelViewerInfo.Text = $"{areaName} - {preview.Summary}";
            Dispatcher.UIThread.Post(() =>
            {
                var maxY = Math.Max(0, LevelViewerScrollViewer.Extent.Height - LevelViewerScrollViewer.Viewport.Height);
                LevelViewerScrollViewer.Offset = new Vector(0, maxY);
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            LevelViewerCanvas.Course = null;
            LevelViewerInfo.Text = $"Preview could not be loaded: {ex.Message}";
        }
    }

    private void ShowSavedLevelDetails(LevelInfo level)
    {
        ShowSavedSelectedLevelContainer();
        SavedLevelDetailsPanel.IsVisible = true;
        SavedLevelTitle.Text = $"{level.Name} | {level.DisplayCode} ({level.LevelId})";
        SavedLevelCreatorText.Text = string.IsNullOrWhiteSpace(level.Creator) ? "Local" : level.Creator;
        SavedLevelClearRateText.Text = $"{level.ClearRate * 100:0.##}% ({level.Clears}/{level.TotalAttempts})";
        SavedLevelWorldRecordText.Text = level.WorldRecordMs > 0
            ? $"{FormatTime(level.WorldRecordMs)} by {level.WorldRecordHolderNnid}"
            : "n/a";
        SetSavedStats(level);
        CreatorMiiImage.Source = null;
        CreatorMiiBorder.IsVisible = false;
        WorldRecordMiiImage.Source = null;
        WorldRecordMiiBorder.IsVisible = false;
    }

    private async Task LoadSavedLevelMiiImagesAsync(LevelInfo level)
    {
        var creator = await _miiImageLoader.LoadAsync(level.CreatorMiiData, CancellationToken.None);
        if (_selectedPreviewLevel != level)
        {
            return;
        }

        CreatorMiiImage.Source = creator;
        CreatorMiiBorder.IsVisible = creator != null;
        level.CreatorMiiImage = creator;

        var worldRecord = await _miiImageLoader.LoadAsync(level.WorldRecordBestTimePlayerMiiData, CancellationToken.None);
        if (_selectedPreviewLevel != level)
        {
            return;
        }

        WorldRecordMiiImage.Source = worldRecord;
        WorldRecordMiiBorder.IsVisible = worldRecord != null;
        level.WorldRecordMiiImage = worldRecord;
    }

    private void ApplySavedFilter()
    {
        var filter = SavedSearchTextBox.Text?.Trim() ?? "";
        _savedNodes.Clear();
        foreach (var node in FilterSavedNodes(_allSavedNodes, filter))
        {
            _savedNodes.Add(node);
        }
    }

    private static IEnumerable<SavedLevelNode> FilterSavedNodes(IEnumerable<SavedLevelNode> nodes, string filter)
    {
        foreach (var node in nodes)
        {
            if (node.Level != null)
            {
                if (MatchesFilter(node.Level, filter))
                {
                    yield return node;
                }

                continue;
            }

            var children = new ObservableCollection<SavedLevelNode>(FilterSavedNodes(node.Children, filter));
            if (string.IsNullOrWhiteSpace(filter) ||
                node.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                children.Count > 0)
            {
                yield return new SavedLevelNode
                {
                    DisplayName = node.DisplayName,
                    Summary = $"{children.Count} course(s)",
                    ShortInfo = $"{children.Count} course(s)",
                    IsFolder = node.IsFolder,
                    StatsHidden = node.StatsHidden,
                    PackName = node.PackName,
                    PackFolder = node.PackFolder,
                    Children = children
                };
            }
        }
    }

    private static bool MatchesFilter(LevelInfo level, string filter)
    {
        return string.IsNullOrWhiteSpace(filter) ||
               level.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               level.Creator.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               level.LevelId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(level.Pack) && level.Pack.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private SavedLevelNode ToLevelNode(LevelInfo level)
    {
        var folder = ResolveCourseFolder(level);
        return new SavedLevelNode
        {
            DisplayName = string.IsNullOrWhiteSpace(level.Name) ? level.LevelId.ToString() : level.Name,
            Summary = level.Summary,
            ShortInfo = BuildSavedLevelShortInfo(level),
            Thumbnail = _thumbnailLoader.LoadCourseThumbnail(folder),
            RowMargin = string.IsNullOrWhiteSpace(level.Pack)
                ? new Thickness(-28, 5, 0, 5)
                : new Thickness(0, 5, 0, 5),
            Level = level
        };
    }

    private string BuildSavedLevelShortInfo(LevelInfo level)
    {
        var creator = string.IsNullOrWhiteSpace(level.Creator) ? "Creator: n/a" : $"Creator: {level.Creator}";
        var worldRecord = level.WorldRecordMs > 0
            ? $"WR: {FormatTime(level.WorldRecordMs)}"
            : "WR: n/a";
        return $"{creator} | {worldRecord}";
    }

    private static bool IsSameSavedLevel(LevelInfo? first, LevelInfo? second)
    {
        return first != null &&
               second != null &&
               first.LevelId == second.LevelId &&
               string.Equals(first.Pack ?? "", second.Pack ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static long TryReadLevelIdFromFolder(string directory)
    {
        var name = Path.GetFileName(directory) ?? "";
        if (long.TryParse(name, out var directId))
        {
            return directId;
        }

        var suffix = name.Split('_').LastOrDefault();
        return long.TryParse(suffix, out var suffixId) ? suffixId : 0;
    }

    private static string GetDisplayNameFromCourseFolder(string directory)
    {
        var name = Path.GetFileName(directory) ?? directory;
        var suffix = name.LastIndexOf('_');
        return suffix > 0 && long.TryParse(name[(suffix + 1)..], out _)
            ? name[..suffix].Replace('_', ' ')
            : name.Replace('_', ' ');
    }

    private string GetSelectedSavedRoot()
    {
        return GetSavedSource() switch
        {
            "official" => Path.Combine(_paths.OfficialCoursesDirectory, "CourseFiles"),
            "packs" => _paths.LevelPacksDirectory,
            "cemu" => GetCemuProfilePath(),
            "backupped" => _paths.BackuppedDirectory,
            _ => _paths.DownloadCacheDirectory
        };
    }

    private string GetSavedSource()
    {
        return (SavedSourceComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "downloaded";
    }

    private string GetCemuProfilePath()
    {
        var profile = ProfileComboBox.SelectedItem?.ToString() ?? _settings.SelectedProfile;
        return string.IsNullOrWhiteSpace(_settings.CemuDirPath) || string.IsNullOrWhiteSpace(profile)
            ? ""
            : Path.Combine(_settings.CemuDirPath, "mlc01", "usr", "save", "00050000", "1018dd00", "user", profile);
    }

    private string? GetPackFolderIfEnabled()
    {
        if (DownloadToPackCheckBox.IsChecked != true)
        {
            return null;
        }

        var packName = string.IsNullOrWhiteSpace(PackNameTextBox.Text) ? "Default Pack" : PackNameTextBox.Text.Trim();
        var packs = _store.LoadLevelPacks();
        if (!packs.TryGetValue(packName, out var folder))
        {
            folder = SanitizeFolderName(packName);
            packs[packName] = folder;
            _store.SaveLevelPacks(packs);
            RefreshPackNameSuggestions();
        }

        Directory.CreateDirectory(Path.Combine(_paths.LevelPacksDirectory, folder));
        return folder;
    }

    private async Task RunSafeAsync(Func<CancellationToken, Task> action)
    {
        try
        {
            _workCts?.Cancel();
            _workCts = new CancellationTokenSource();
            await action(_workCts.Token);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            DownloadStatusText.Text = ex.Message;
        }
    }

    private void SetBusy(string message)
    {
        SetStatus(message);
        DownloadStatusText.Text = message;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void ApplyViewerInfoVisibility()
    {
        CoursePreviewInfo.IsVisible = !_settings.HideViewerInfo;
        LevelViewerInfo.IsVisible = !_settings.HideViewerInfo;
    }

    private async Task RunApiStatusLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await CheckApiStatusAsync(token);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task CheckApiStatusAsync(CancellationToken token)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(6));
            using var response = await _statusHttpClient.GetAsync(ApiPingUrl, timeoutCts.Token);
            _isApiOnline = response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            _isApiOnline = false;
        }

        Dispatcher.UIThread.Post(UpdateApiStatusUi);
    }

    private async Task CheckForUpdatesAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            using var request = new HttpRequestMessage(HttpMethod.Get, GithubLatestReleaseUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", "SMM1-Level-Downloader-Avalonia");
            using var response = await _statusHttpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            var root = document.RootElement;
            var latestTag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(latestTag) ||
                string.Equals(latestTag, CurrentReleaseTag, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _availableUpdateVersion = latestTag;
            _availableUpdateUrl = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : "https://github.com/HerobrineTV/SMM1-Level-Downloader/releases/latest";
            Dispatcher.UIThread.Post(UpdateReleaseStatusUi);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch
        {
        }
    }

    private void UpdateApiStatusUi()
    {
        ApiStatusText.Text = _isApiOnline switch
        {
            true => T("ApiOnline"),
            false => T("ApiOffline"),
            _ => T("ApiChecking")
        };
        ApiStatusDot.Background = _isApiOnline switch
        {
            true => Brushes.ForestGreen,
            false => Brushes.Firebrick,
            _ => Brushes.Goldenrod
        };
    }

    private void UpdateReleaseStatusUi()
    {
        var hasUpdate = !string.IsNullOrWhiteSpace(_availableUpdateUrl);
        UpdateButton.IsVisible = hasUpdate;
        if (hasUpdate)
        {
            UpdateButton.Content = string.IsNullOrWhiteSpace(_availableUpdateVersion)
                ? T("UpdateAvailable")
                : $"{T("UpdateAvailable")}: {_availableUpdateVersion}";
        }
    }

    private static string SanitizeFolderName(string value)
    {
        var chars = value.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        return string.Concat(chars).Trim('_');
    }

    private static string FormatTime(long milliseconds)
    {
        if (milliseconds <= 0)
        {
            return "n/a";
        }

        var time = TimeSpan.FromMilliseconds(milliseconds);
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    private string FormatUploadTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "n/a";
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var date))
        {
            var local = date.ToLocalTime();
            return string.Equals(_settings.Language, "de", StringComparison.OrdinalIgnoreCase)
                ? local.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-DE"))
                : local.ToString("MMM d, yyyy h:mm tt", CultureInfo.GetCultureInfo("en-US"));
        }

        return value.Trim();
    }

    private static string FormatName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "n/a" : value.Trim();
    }

    private static readonly Dictionary<string, string> EnglishText = new()
    {
        ["DownloadTab"] = "Download",
        ["SavedCoursesTab"] = "Saved Courses",
        ["SettingsTab"] = "Settings",
        ["Back"] = "Back",
        ["SearchCourses"] = "Search Courses",
        ["SearchWatermark"] = "Level name, level ID, creator name or creator ID",
        ["Search"] = "Search",
        ["Random"] = "Random",
        ["LevelName"] = "Level Name",
        ["LevelId"] = "Level ID",
        ["CreatorName"] = "Creator Name",
        ["CreatorId"] = "Creator ID",
        ["Exact"] = "Exact",
        ["SaveInLevelPack"] = "Save in Level Pack",
        ["LevelPackName"] = "Level Pack Name",
        ["DownloadAllResults"] = "Download All Results",
        ["SelectedCourse"] = "Selected Course",
        ["SelectedLevel"] = "Selected Level",
        ["NoCourseSelected"] = "No course selected",
        ["NoLevelSelected"] = "No level selected",
        ["Creator"] = "Creator",
        ["WorldRecord"] = "World Record",
        ["UserProfile"] = "User Profile",
        ["LoadProfile"] = "Load Profile",
        ["ProfileSearchHint"] = "Search a creator to list their levels.",
        ["SavedCourses"] = "Saved Courses",
        ["DownloadedInCache"] = "Downloaded in Cache",
        ["LevelPacks"] = "Level Packs",
        ["OfficialTestingCourses"] = "Official Testing Courses",
        ["CemuProfileCourses"] = "Cemu Profile Courses",
        ["BackuppedCourses"] = "Backupped Courses",
        ["FilterSavedCourses"] = "Filter saved courses",
        ["Refresh"] = "Refresh",
        ["RefreshData"] = "Refresh Data",
        ["OpenLevelFolder"] = "Open Level Folder",
        ["OpenFolder"] = "Open Folder",
        ["DeleteSelected"] = "Delete Selected",
        ["Delete"] = "Delete",
        ["Cancel"] = "Cancel",
        ["RemoveCoursePackFolder"] = "Remove Course Pack Folder",
        ["RemovePackTitle"] = "Delete Level Pack",
        ["RemovePackWarning"] = "Delete the level pack '{0}'?\n\nAll courses from this level pack will continue to exist without a level pack and will be moved back to the main saved courses folder if they are not already there.",
        ["ResetOfficialCourses"] = "Reset Official Courses",
        ["SelectSavedCourse"] = "Select a saved course",
        ["CoursePreviewHint"] = "The course preview will appear below.",
        ["Overworld"] = "Overworld",
        ["Underworld"] = "Underworld",
        ["RevealHiddenBlocks"] = "Reveal Hidden ? Blocks",
        ["HideHiddenBlocks"] = "Hide Hidden ? Blocks",
        ["ClearRate"] = "Clear Rate",
        ["Settings"] = "Settings",
        ["Language"] = "Language",
        ["UseCemuFolder"] = "Use Cemu Folder",
        ["CemuFolderPath"] = "Cemu folder path",
        ["SelectFolder"] = "Select Folder",
        ["HideViewerInfo"] = "Hide Viewer Info",
        ["UseProxy"] = "Use Proxy for Downloads",
        ["OpenProxyFile"] = "Open Proxy File",
        ["ApiEndpoint"] = "API Endpoint",
        ["SaveSettings"] = "Save Settings",
        ["ResetSettings"] = "Reset Settings",
        ["RefreshAllDownloadedData"] = "Refresh All Downloaded Course Data",
        ["ApiChecking"] = "API: Checking",
        ["ApiOnline"] = "API: Online",
        ["ApiOffline"] = "API: Offline",
        ["UpdateAvailable"] = "Update available",
        ["SettingsSaved"] = "Settings saved.",
        ["SettingsReset"] = "Settings reset."
    };

    private static readonly Dictionary<string, string> GermanText = new()
    {
        ["DownloadTab"] = "Download",
        ["SavedCoursesTab"] = "Gespeicherte Level",
        ["SettingsTab"] = "Einstellungen",
        ["Back"] = "Zurueck",
        ["SearchCourses"] = "Level suchen",
        ["SearchWatermark"] = "Levelname, Level-ID, Erstellername oder Ersteller-ID",
        ["Search"] = "Suchen",
        ["Random"] = "Zufall",
        ["LevelName"] = "Levelname",
        ["LevelId"] = "Level-ID",
        ["CreatorName"] = "Erstellername",
        ["CreatorId"] = "Ersteller-ID",
        ["Exact"] = "Exakt",
        ["SaveInLevelPack"] = "In Level-Pack speichern",
        ["LevelPackName"] = "Level-Pack-Name",
        ["DownloadAllResults"] = "Alle Ergebnisse laden",
        ["SelectedCourse"] = "Ausgewaehltes Level",
        ["SelectedLevel"] = "Ausgewaehltes Level",
        ["NoCourseSelected"] = "Kein Level ausgewaehlt",
        ["NoLevelSelected"] = "Kein Level ausgewaehlt",
        ["Creator"] = "Ersteller",
        ["WorldRecord"] = "Weltrekord",
        ["UserProfile"] = "Nutzerprofil",
        ["LoadProfile"] = "Profil laden",
        ["ProfileSearchHint"] = "Suche einen Ersteller, um dessen Level aufzulisten.",
        ["SavedCourses"] = "Gespeicherte Level",
        ["DownloadedInCache"] = "Im Cache geladen",
        ["LevelPacks"] = "Level-Packs",
        ["OfficialTestingCourses"] = "Offizielle Test-Level",
        ["CemuProfileCourses"] = "Cemu-Profil-Level",
        ["BackuppedCourses"] = "Gesicherte Level",
        ["FilterSavedCourses"] = "Gespeicherte Level filtern",
        ["Refresh"] = "Aktualisieren",
        ["RefreshData"] = "Daten aktualisieren",
        ["OpenLevelFolder"] = "Level-Ordner oeffnen",
        ["OpenFolder"] = "Ordner oeffnen",
        ["DeleteSelected"] = "Auswahl loeschen",
        ["Delete"] = "Loeschen",
        ["Cancel"] = "Abbrechen",
        ["RemoveCoursePackFolder"] = "Level-Pack-Ordner entfernen",
        ["RemovePackTitle"] = "Level-Pack loeschen",
        ["RemovePackWarning"] = "Level-Pack '{0}' loeschen?\n\nAlle Level aus diesem Level-Pack bleiben weiterhin ohne Level-Pack erhalten und werden in den normalen gespeicherten Level-Ordner verschoben, falls sie dort noch nicht vorhanden sind.",
        ["ResetOfficialCourses"] = "Offizielle Level zuruecksetzen",
        ["SelectSavedCourse"] = "Gespeichertes Level auswaehlen",
        ["CoursePreviewHint"] = "Die Level-Vorschau erscheint darunter.",
        ["Overworld"] = "Oberwelt",
        ["Underworld"] = "Unterwelt",
        ["RevealHiddenBlocks"] = "Versteckte ?-Bloecke zeigen",
        ["HideHiddenBlocks"] = "Versteckte ?-Bloecke ausblenden",
        ["ClearRate"] = "Clear-Rate",
        ["Settings"] = "Einstellungen",
        ["Language"] = "Sprache",
        ["UseCemuFolder"] = "Cemu-Ordner verwenden",
        ["CemuFolderPath"] = "Cemu-Ordnerpfad",
        ["SelectFolder"] = "Ordner waehlen",
        ["HideViewerInfo"] = "Viewer-Info ausblenden",
        ["UseProxy"] = "Proxy fuer Downloads verwenden",
        ["OpenProxyFile"] = "Proxy-Datei oeffnen",
        ["ApiEndpoint"] = "API-Endpunkt",
        ["SaveSettings"] = "Einstellungen speichern",
        ["ResetSettings"] = "Einstellungen zuruecksetzen",
        ["RefreshAllDownloadedData"] = "Alle geladenen Leveldaten aktualisieren",
        ["ApiChecking"] = "API: Pruefe",
        ["ApiOnline"] = "API: Online",
        ["ApiOffline"] = "API: Offline",
        ["UpdateAvailable"] = "Update verfuegbar",
        ["SettingsSaved"] = "Einstellungen gespeichert.",
        ["SettingsReset"] = "Einstellungen zurueckgesetzt."
    };

    private sealed class DownloadState
    {
        public bool IsDownloading { get; set; }
        public bool IsDownloaded { get; set; }
        public string ProgressText { get; set; } = "";
    }
}
