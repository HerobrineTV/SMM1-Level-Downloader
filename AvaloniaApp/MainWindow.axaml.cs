using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    private const string CurrentReleaseTag = "Pre_1__V2.0.0";
    private const string LevelBackupsFolderName = "LevelBackups";

    private readonly ProjectPaths _paths = new();
    private readonly JsonStore _store;
    private readonly DataMigrationService _dataMigrationService;
    private readonly LevelApiClient _apiClient = new();
    private readonly LevelDownloadService _downloadService;
    private readonly SmmCourseParser _courseParser = new();
    private readonly CemuSaveService _cemuSaveService;
    private readonly StandardSoundService _standardSoundService;
    private readonly ThumbnailLoader _thumbnailLoader = new();
    private readonly MiiImageLoader _miiImageLoader = new();
    private readonly HttpClient _statusHttpClient = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly ObservableCollection<object> _searchResults = [];
    private readonly LoadMoreSearchResultsItem _loadMoreSearchResultsItem = new();
    private readonly ObservableCollection<LevelInfo> _profileLevels = [];
    private readonly ObservableCollection<SavedLevelNode> _savedNodes = [];
    private readonly ObservableCollection<SavedLevelNode> _cemuNodes = [];
    private readonly ObservableCollection<LevelInfo> _cemuReplaceResults = [];
    private readonly ObservableCollection<NotificationEntry> _notifications = [];
    private readonly Dictionary<long, DownloadState> _downloadStates = [];
    private IReadOnlyList<LevelInfo> _allSavedLevels = [];
    private IReadOnlyList<SavedLevelNode> _allSavedNodes = [];
    private AppSettings _settings = new();
    private CancellationTokenSource? _workCts;
    private string _currentSearchPhrase = "";
    private int _currentSearchPage = 1;
    private bool _isLoadingSearchPage;
    private bool _hasMoreSearchPages;
    private LevelInfo? _selectedSearchResult;
    private string _currentProfileUserName = "";
    private LevelInfo? _selectedPreviewLevel;
    private SavedLevelNode? _selectedSavedNode;
    private SavedLevelNode? _selectedPackNode;
    private SavedLevelNode? _selectedCemuNode;
    private LevelInfo? _selectedCemuReplacementLevel;
    private string _selectedPreviewFile = "course_data.cdt";
    private string _selectedCemuPreviewFile = "course_data.cdt";
    private LevelInfo? _largeViewerLevel;
    private string _largeViewerFile = "course_data.cdt";
    private bool _returnToProfilePageFromViewer;
    private bool _returnToCemuLevelPageFromProfile;
    private bool? _isApiOnline;
    private string? _availableUpdateVersion;
    private string? _availableUpdateUrl;
    private bool _startupInitialized;
    private bool _isApplyingPackNameSuggestion;
    private bool _isRefreshingAllDownloadedData;
    private bool _isLoadingCemuProfiles;
    private bool _isLoadingSettings;
    private int _notificationSequence;

    public MainWindow()
    {
        InitializeComponent();

        _store = new JsonStore(_paths);
        _dataMigrationService = new DataMigrationService(_paths);
        _downloadService = new LevelDownloadService(_paths, _store);
        _cemuSaveService = new CemuSaveService(_paths, _courseParser);
        _standardSoundService = new StandardSoundService(_paths, _cemuSaveService);
        SearchResultsListBox.ItemsSource = _searchResults;
        ProfileLevelsListBox.ItemsSource = _profileLevels;
        SavedLevelsTreeView.ItemsSource = _savedNodes;
        CemuLevelsItemsControl.ItemsSource = _cemuNodes;
        CemuReplaceResultsListBox.ItemsSource = _cemuReplaceResults;
        NotificationHistoryListBox.ItemsSource = _notifications;
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
        _settings = _store.LoadSettings();
        EnsureStartupCourseSounds();
        InitializeDataBackedUi();
        ResetPrereleaseWarningOptOutIfStableRelease();
        await ShowPrereleaseWarningIfNeededAsync();
    }

    private void EnsureStartupCourseSounds()
    {
        try
        {
            var updated = _standardSoundService.EnsureExistingCourseSounds(_cemuSaveService.ResolveCemuDirectory());
            if (updated > 0)
            {
                SetStatus($"Added missing sound.bwv to {updated} course folder(s).");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Could not prepare standard sound file: {ex.Message}");
        }
    }

    private void InitializeDataBackedUi()
    {
        LoadSettingsIntoUi();
        ApplyLanguage();
        RefreshPackNameSuggestions();
        ResetSearchSelectedLevelDetails();
        ResetProfileSelectedLevelDetails();
        LoadSavedLevels();
        RefreshCemuLevels();
        _ = LoadCreditImagesAsync(_lifetimeCts.Token);
        _ = RunApiStatusLoopAsync(_lifetimeCts.Token);
        _ = CheckForUpdatesAsync(_lifetimeCts.Token);
        _ = RegisterFirstStartIfNeededAsync(_lifetimeCts.Token);
        _ = QueueStartupDownloadedDataRefreshAsync(_lifetimeCts.Token);
    }

    private async Task RegisterFirstStartIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_settings.FirstStartRegistered)
        {
            return;
        }

        try
        {
            await _apiClient.RegisterFirstStartAsync(_settings, cancellationToken);
            _settings.FirstStartRegistered = true;
            _store.SaveSettings(_settings);
        }
        catch
        {
            // Analytics must not block startup. A failed registration is retried on the next start.
        }
    }

    private async Task QueueStartupDownloadedDataRefreshAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (string.Equals(_settings.LastFullRefresh, today, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            await RefreshAllDownloadedDataAsync(cancellationToken, isStartupRefresh: true);
            _settings.LastFullRefresh = today;
            _store.SaveSettings(_settings);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Startup metadata refresh failed: {ex.Message}");
        }
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

    private async Task ShowPrereleaseWarningIfNeededAsync()
    {
        if (!CurrentReleaseTag.Contains("Pre", StringComparison.OrdinalIgnoreCase) ||
            _settings.HidePrereleaseWarning)
        {
            return;
        }

        var dontShowAgainCheckBox = new CheckBox
        {
            Content = T("DontShowAgain"),
            Foreground = new SolidColorBrush(Color.Parse("#2B1605"))
        };

        var okButton = new Button
        {
            Content = T("Ok"),
            MinWidth = 90,
            Padding = new Thickness(13, 8),
            Background = new SolidColorBrush(Color.Parse("#F9C74F")),
            Foreground = new SolidColorBrush(Color.Parse("#2B1605")),
            BorderBrush = new SolidColorBrush(Color.Parse("#8F4F17")),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var dialog = new Window
        {
            Title = T("PrereleaseWarningTitle"),
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FFF8D6")),
                BorderBrush = new SolidColorBrush(Color.Parse("#8F4F17")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = T("PrereleaseWarningTitle"),
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#2B1605"))
                        },
                        new TextBlock
                        {
                            Text = T("PrereleaseWarningMessage"),
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#2B1605"))
                        },
                        dontShowAgainCheckBox,
                        okButton
                    }
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close(dontShowAgainCheckBox.IsChecked == true);
        var hideWarning = await dialog.ShowDialog<bool>(this);
        if (hideWarning)
        {
            _settings.HidePrereleaseWarning = true;
            _store.SaveSettings(_settings);
        }
    }

    private void ResetPrereleaseWarningOptOutIfStableRelease()
    {
        if (CurrentReleaseTag.Contains("Pre", StringComparison.OrdinalIgnoreCase) ||
            !_settings.HidePrereleaseWarning)
        {
            return;
        }

        _settings.HidePrereleaseWarning = false;
        _store.SaveSettings(_settings);
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
            UpdateLoadMoreSearchResultsButton();
            _settings.LastSearchPhrase = phrase;
            _settings.RecentFoundLevels = results.ToDictionary(level => level.LevelId.ToString());
            _store.SaveSettings(_settings);
            SetStatus($"Found {SearchResultCount} courses.");
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
            UpdateLoadMoreSearchResultsButton();
            SetStatus($"Loaded {SearchResultCount} random result(s).");
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

    private async void LoadMoreSearchResultsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await LoadNextSearchPageAsync();
    }

    private async Task LoadNextSearchPageAsync()
    {
        if (!_hasMoreSearchPages || _isLoadingSearchPage || string.IsNullOrWhiteSpace(_currentSearchPhrase))
        {
            UpdateLoadMoreSearchResultsButton();
            return;
        }

        try
        {
            _isLoadingSearchPage = true;
            UpdateLoadMoreSearchResultsButton();
            SaveSettingsFromUi();
            var nextPage = _currentSearchPage + 1;
            SetStatus($"Loading search page {nextPage}...");
            var results = await _apiClient.SearchAsync(_settings, _currentSearchPhrase, nextPage, CancellationToken.None);
            if (results.Count == 0)
            {
                _hasMoreSearchPages = false;
                SetStatus($"Loaded all {SearchResultCount} search results.");
                return;
            }

            var existingIds = SearchResultLevels.Select(level => level.LevelId).ToHashSet();
            var added = 0;
            foreach (var level in results.Where(level => existingIds.Add(level.LevelId)))
            {
                AddSearchResult(level);
                added++;
            }

            if (added == 0)
            {
                _hasMoreSearchPages = false;
                SetStatus($"Loaded all {SearchResultCount} search results.");
                return;
            }

            _currentSearchPage = nextPage;
            SetStatus($"Loaded {SearchResultCount} courses.");
        }
        catch (Exception ex)
        {
            _hasMoreSearchPages = false;
            SetStatus(ex.Message);
        }
        finally
        {
            _isLoadingSearchPage = false;
            UpdateLoadMoreSearchResultsButton();
        }
    }

    private void UpdateLoadMoreSearchResultsButton()
    {
        var shouldShow = _hasMoreSearchPages && !string.IsNullOrWhiteSpace(_currentSearchPhrase);
        _loadMoreSearchResultsItem.Text = T("LoadMore");
        _loadMoreSearchResultsItem.IsEnabled = shouldShow && !_isLoadingSearchPage;

        if (shouldShow)
        {
            if (!_searchResults.Contains(_loadMoreSearchResultsItem))
            {
                _searchResults.Add(_loadMoreSearchResultsItem);
            }
        }
        else
        {
            _searchResults.Remove(_loadMoreSearchResultsItem);
        }
    }

    private async Task OpenProfileAsync(string? userName, IImage? miiImage, bool returnToCemuLevelPage = false)
    {
        userName = userName?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        await RunSafeAsync(async token =>
        {
            SetBusy($"Loading profile for {userName}...");
            _returnToCemuLevelPageFromProfile = returnToCemuLevelPage ||
                                                (_returnToCemuLevelPageFromProfile && ProfilePage.IsVisible);
            MainTabs.IsVisible = false;
            CemuLevelPage.IsVisible = false;
            LevelViewerPage.IsVisible = false;
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

    private async void CemuCreatorMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_selectedCemuNode?.Level is { } level)
        {
            await OpenProfileAsync(level.Creator, level.CreatorMiiImage, returnToCemuLevelPage: true);
            e.Handled = true;
        }
    }

    private async void CemuWorldRecordMii_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_selectedCemuNode?.Level is { } level)
        {
            await OpenProfileAsync(level.WorldRecordHolderNnid, level.WorldRecordMiiImage, returnToCemuLevelPage: true);
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
        if (_returnToCemuLevelPageFromProfile)
        {
            CemuLevelPage.IsVisible = true;
            _returnToCemuLevelPageFromProfile = false;
        }
        else
        {
            MainTabs.IsVisible = true;
        }
    }

    private void BackFromCemuLevelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CemuLevelPage.IsVisible = false;
        MainTabs.IsVisible = true;
        MainTabs.SelectedItem = CemuTab;
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

    private void LevelViewerUnderworldButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_largeViewerLevel == null)
        {
            return;
        }

        if (!HasUnderworld(_largeViewerLevel, ResolveDownloadedCourseFolder))
        {
            LevelViewerUnderworldButton.IsVisible = false;
            _largeViewerFile = "course_data.cdt";
            LoadLargeLevelViewer(_largeViewerLevel, _largeViewerFile);
            return;
        }

        _largeViewerFile = _largeViewerFile == "course_data_sub.cdt"
            ? "course_data.cdt"
            : "course_data_sub.cdt";
        LoadLargeLevelViewer(_largeViewerLevel, _largeViewerFile);
    }

    private void LevelViewerHiddenBlocksButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LevelViewerCanvas.ShowHiddenBlocks = !LevelViewerCanvas.ShowHiddenBlocks;
        LevelViewerHiddenBlocksButton.Content = LevelViewerCanvas.ShowHiddenBlocks
            ? T("HideHiddenBlocks")
            : T("RevealHiddenBlocks");
    }

    private void CemuPageUnderworldButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedCemuNode?.Level == null)
        {
            return;
        }

        if (!HasUnderworld(_selectedCemuNode.Level, selected => selected.Folder))
        {
            CemuPageUnderworldButton.IsVisible = false;
            _selectedCemuPreviewFile = "course_data.cdt";
            LoadCemuPagePreview(_selectedCemuNode.Level, _selectedCemuPreviewFile);
            return;
        }

        _selectedCemuPreviewFile = _selectedCemuPreviewFile == "course_data_sub.cdt"
            ? "course_data.cdt"
            : "course_data_sub.cdt";
        LoadCemuPagePreview(_selectedCemuNode.Level, _selectedCemuPreviewFile);
    }

    private void CemuPageHiddenBlocksButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CemuPagePreviewCanvas.ShowHiddenBlocks = !CemuPagePreviewCanvas.ShowHiddenBlocks;
        CemuPageHiddenBlocksButton.Content = CemuPagePreviewCanvas.ShowHiddenBlocks
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
        ProfileTotalDownloadsText.Text = "Downloads: 0";
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
        UnderworldPreviewButton.IsVisible = true;
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
        ProfileTotalDownloadsText.Text = $"Downloads: {levels.Sum(level => level.Downloads)}";
        ProfileTotalAttemptsText.Text = $"Attempts: {totalAttempts}";
        ProfileTotalClearsText.Text = $"Clears: {totalClears}";
        ProfileAverageClearRateText.Text = $"Clear Rate: {clearRate}";
    }

    private void SetSelectedStats(LevelInfo level)
    {
        SelectedDownloadsText.Text = $"DL Downloads: {level.DownloadsText}";
        SelectedStarsText.Text = $"★ Stars: {level.StarsText}";
        SelectedTotalRunsText.Text = $"▶ Total Runs: {level.TotalAttemptsText}";
        SelectedClearsText.Text = $"✓ Clears: {level.ClearsText}";
        SelectedClearRateText.Text = $"% Clear Rate: {level.ClearRateText}";
    }

    private void SetProfileSelectedStats(LevelInfo level)
    {
        ProfileSelectedDownloadsText.Text = $"DL Downloads: {level.DownloadsText}";
        ProfileSelectedStarsText.Text = $"★ Stars: {level.StarsText}";
        ProfileSelectedTotalRunsText.Text = $"▶ Total Runs: {level.TotalAttemptsText}";
        ProfileSelectedClearsText.Text = $"✓ Clears: {level.ClearsText}";
        ProfileSelectedClearRateText.Text = $"% Clear Rate: {level.ClearRateText}";
    }

    private void SetSavedStats(LevelInfo level)
    {
        SavedDownloadsText.Text = $"DL Downloads: {level.DownloadsText}";
        SavedStarsText.Text = $"★ Stars: {level.StarsText}";
        SavedTotalRunsText.Text = $"▶ Total Runs: {level.TotalAttemptsText}";
        SavedClearsText.Text = $"✓ Clears: {level.ClearsText}";
    }

    private void UpdateSavedPreviewAreaButtons(LevelInfo level)
    {
        var hasUnderworld = HasUnderworld(level, ResolveCourseFolder);
        UnderworldPreviewButton.IsVisible = hasUnderworld;
        if (!hasUnderworld && _selectedPreviewFile == "course_data_sub.cdt")
        {
            _selectedPreviewFile = "course_data.cdt";
        }

        UnderworldPreviewButton.Content = GetAreaSwitchText(_selectedPreviewFile);
    }

    private void UpdateLargeViewerAreaButtons(LevelInfo level)
    {
        var hasUnderworld = HasUnderworld(level, ResolveDownloadedCourseFolder);
        LevelViewerUnderworldButton.IsVisible = hasUnderworld;
        if (!hasUnderworld && _largeViewerFile == "course_data_sub.cdt")
        {
            _largeViewerFile = "course_data.cdt";
        }

        LevelViewerUnderworldButton.Content = GetAreaSwitchText(_largeViewerFile);
    }

    private void UpdateCemuPageAreaButtons(LevelInfo level)
    {
        var hasUnderworld = HasUnderworld(level, selected => selected.Folder);
        CemuPageUnderworldButton.IsVisible = hasUnderworld;
        if (!hasUnderworld && _selectedCemuPreviewFile == "course_data_sub.cdt")
        {
            _selectedCemuPreviewFile = "course_data.cdt";
        }

        CemuPageUnderworldButton.Content = GetAreaSwitchText(_selectedCemuPreviewFile);
    }

    private string GetAreaSwitchText(string currentCourseFileName)
    {
        return currentCourseFileName == "course_data_sub.cdt" ? T("Overworld") : T("Underworld");
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
        foreach (var level in SearchResultLevels.ToList())
        {
            if (CanStartDownload(level))
            {
                await DownloadLevelAsync(level);
            }
        }
    }

    private void SearchResultsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is LoadMoreSearchResultsItem)
        {
            SearchResultsListBox.SelectedItem = _selectedSearchResult;
            return;
        }

        if (SearchResultsListBox.SelectedItem is not LevelInfo level)
        {
            _selectedSearchResult = null;
            ResetSearchSelectedLevelDetails();
            return;
        }

        _selectedSearchResult = level;
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
            UpdateSavedPreviewAreaButtons(level);
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

    private void UnderworldPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPreviewLevel == null)
        {
            return;
        }

        if (!HasUnderworld(_selectedPreviewLevel, ResolveCourseFolder))
        {
            UnderworldPreviewButton.IsVisible = false;
            _selectedPreviewFile = "course_data.cdt";
            LoadCoursePreview(_selectedPreviewLevel, _selectedPreviewFile);
            return;
        }

        _selectedPreviewFile = _selectedPreviewFile == "course_data_sub.cdt"
            ? "course_data.cdt"
            : "course_data_sub.cdt";
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

    private void ProfileComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingCemuProfiles)
        {
            return;
        }

        SaveSettingsFromUi();
        RefreshCemuLevels();
        if (GetSavedSource() == "cemu")
        {
            LoadSavedLevels();
        }
    }

    private void RefreshCemuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        RefreshCemuLevels();
    }

    private void CemuLevelsScrollViewer_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateCemuCardWidth();
    }

    private void OpenCemuFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopIntegration.OpenPath(GetCemuProfilePath());
    }

    private void CemuLevelCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SavedLevelNode { Level: { } level } node)
        {
            _selectedCemuNode = node;
            _selectedCemuReplacementLevel = null;
            CemuReplaceResultsListBox.SelectedItem = null;
            CemuReplaceSearchTextBox.Text = "";
            OpenCemuLevelPage(level);
            LoadCemuReplaceResults("");
            e.Handled = true;
        }
    }

    private void CemuReplaceSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        LoadCemuReplaceResults(CemuReplaceSearchTextBox.Text?.Trim() ?? "");
    }

    private void CemuReplaceResultsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedCemuReplacementLevel = CemuReplaceResultsListBox.SelectedItem as LevelInfo;
        UpdateReplaceCemuLevelButtonState();
    }

    private async void ReplaceCemuLevelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedCemuNode?.Level is not { } cemuLevel || _selectedCemuReplacementLevel == null)
        {
            return;
        }

        var replacementFolder = ResolveDownloadedCourseFolder(_selectedCemuReplacementLevel);
        if (replacementFolder == null)
        {
            SetStatus("Selected replacement level is no longer available.");
            return;
        }

        var confirmed = await ShowConfirmDialogAsync(
            T("ReplaceCemuLevelTitle"),
            string.Format(CultureInfo.InvariantCulture, T("ReplaceCemuLevelWarning"), cemuLevel.Name, _selectedCemuReplacementLevel.Name),
            T("Replace"),
            T("Cancel"));
        if (!confirmed)
        {
            return;
        }

        await RunSafeAsync(_ =>
        {
            var cemuFolder = cemuLevel.Folder;
            _standardSoundService.EnsureCourseSoundFile(replacementFolder);
            var backupFolder = _cemuSaveService.BackupAndReplace(cemuLevel, _selectedCemuReplacementLevel, replacementFolder);
            _thumbnailLoader.InvalidateCourseThumbnail(cemuFolder);
            _thumbnailLoader.InvalidateCourseThumbnail(backupFolder);
            RefreshCemuLevels();
            LoadSavedLevels();
            var refreshedNode = _cemuNodes.FirstOrDefault(node =>
                node.Level != null &&
                string.Equals(node.Level.Folder, cemuFolder, StringComparison.OrdinalIgnoreCase));
            if (refreshedNode?.Level != null)
            {
                _selectedCemuNode = refreshedNode;
                OpenCemuLevelPage(refreshedNode.Level);
                LoadCemuReplaceResults(CemuReplaceSearchTextBox.Text?.Trim() ?? "");
            }

            SetStatus($"Replaced CEMU level. Backup: {backupFolder}");
            return Task.CompletedTask;
        });
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
        await RunSafeAsync(token => RefreshAllDownloadedDataAsync(token, isStartupRefresh: false));
    }

    private async Task RefreshAllDownloadedDataAsync(CancellationToken token, bool isStartupRefresh)
    {
        if (_isRefreshingAllDownloadedData)
        {
            if (!isStartupRefresh)
            {
                SetStatus("Downloaded metadata refresh is already running.");
            }

            return;
        }

        SaveSettingsFromUi();
        var downloaded = _store.LoadDownloaded();
        var entries = downloaded.ToList();
        if (entries.Count == 0)
        {
            if (!isStartupRefresh)
            {
                SetStatus("No downloaded courses to refresh.");
            }

            return;
        }

        _isRefreshingAllDownloadedData = true;
        RefreshAllDownloadedDataButton.IsEnabled = false;
        try
        {
            var refreshed = 0;
            var failed = 0;
            var prefix = isStartupRefresh ? "Startup refresh" : "Refreshing";
            for (var i = 0; i < entries.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var (key, oldLevel) = entries[i];
                SetStatus($"{prefix} {i + 1}/{entries.Count}: {oldLevel.LevelId}...");

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
            _isRefreshingAllDownloadedData = false;
            RefreshAllDownloadedDataButton.IsEnabled = true;
        }
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

    private void SaveSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        _store.SaveSettings(_settings);
        ApplyLanguage();
        RefreshCemuLevels();
        SetStatus(T("SettingsSaved"));
    }

    private void ResetSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _settings = new AppSettings();
        LoadSettingsIntoUi();
        _store.SaveSettings(_settings);
        ApplyLanguage();
        RefreshCemuLevels();
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

    private void DebugSettingsCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (!IsInitialized || _isLoadingSettings)
        {
            return;
        }

        SaveDebugSettingsFromUi();
        ApplyDebugSettings();
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
            DesktopIntegration.OpenUrl(_availableUpdateUrl);
        }
    }

    private void HerobrineGithubButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopIntegration.OpenUrl("https://github.com/HerobrineTV");
    }

    private void HerobrineDiscordButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopIntegration.OpenUrl("https://discord.com/invite/5bbpH2SpGN");
    }

    private void HerobrineTwitterButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopIntegration.OpenUrl("https://twitter.com/HerobrineTVv");
    }

    private void KofiButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopIntegration.OpenUrl("https://ko-fi.com/herobrinetvv");
    }

    private void SnoozbusterGithubButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopIntegration.OpenUrl("https://github.com/snoozbuster");
    }

    private void LeoMauroGithubButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopIntegration.OpenUrl("https://github.com/leomaurodesenv");
    }

    private void NotificationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        NotificationToast.IsVisible = false;
        NotificationHistoryPanel.IsVisible = !NotificationHistoryPanel.IsVisible;
    }

    private void CloseNotificationHistoryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        NotificationHistoryPanel.IsVisible = false;
    }

    private void ClearNotificationHistoryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _notifications.Clear();
        NotificationToast.IsVisible = false;
        UpdateNotificationButtonText();
    }

    private void DeleteNotificationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is NotificationEntry notification)
        {
            _notifications.Remove(notification);
            UpdateNotificationButtonText();
        }
    }

    private void RefreshPackNameSuggestions()
    {
        PackNameTextBox.ItemsSource = _store.LoadLevelPacks()
            .Keys
            .Where(name => !string.Equals(name, LevelBackupsFolderName, StringComparison.OrdinalIgnoreCase))
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
            await RegisterLevelDownloadAsync(level.LevelId, cts.Token);
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
            _standardSoundService.EnsureStandardSoundFile();
            await _downloadService.DownloadAsync(level, packFolder, progress, cts.Token);
            _standardSoundService.EnsureCourseSoundFile(level.Folder);
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

    private async Task RegisterLevelDownloadAsync(long levelId, CancellationToken cancellationToken)
    {
        try
        {
            await _apiClient.RegisterLevelDownloadAsync(_settings, levelId, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
        }
        catch
        {
            // Analytics must not block or fail the actual level download.
        }
    }

    private void LoadSettingsIntoUi()
    {
        _settings = _store.LoadSettings();
        var cemuDirectory = _cemuSaveService.ResolveCemuDirectory();

        _isLoadingSettings = true;
        try
        {
            SearchTextBox.Text = _settings.LastSearchPhrase;
            SearchLevelNameCheckBox.IsChecked = _settings.SearchParams.LevelName;
            SearchLevelIdCheckBox.IsChecked = _settings.SearchParams.LevelID;
            SearchCreatorNameCheckBox.IsChecked = _settings.SearchParams.CreatorName;
            SearchCreatorIdCheckBox.IsChecked = _settings.SearchParams.CreatorID;
            SearchExactCheckBox.IsChecked = _settings.SearchParams.SearchExact;
            HideViewerInfoCheckBox.IsChecked = _settings.HideViewerInfo;
            DebugLevelViewerCheckBox.IsChecked = _settings.Debug.LevelViewer;
            DebugTileRegionsCheckBox.IsChecked = _settings.Debug.TileRegions;
            ShowGridCheckBox.IsChecked = _settings.Debug.ShowGrid;
            UseProxyCheckBox.IsChecked = _settings.UseProxy;
            ApiLinkTextBox.Text = _settings.ApiLink;
            SelectComboBoxItemByTag(LanguageComboBox, _settings.Language);
        }
        finally
        {
            _isLoadingSettings = false;
        }

        LoadProfiles(cemuDirectory);
        ApplyDebugSettings();
    }

    private void SaveSettingsFromUi()
    {
        _settings.LastSearchPhrase = SearchTextBox.Text?.Trim() ?? "";
        _settings.SearchParams.LevelName = SearchLevelNameCheckBox.IsChecked == true;
        _settings.SearchParams.LevelID = SearchLevelIdCheckBox.IsChecked == true;
        _settings.SearchParams.CreatorName = SearchCreatorNameCheckBox.IsChecked == true;
        _settings.SearchParams.CreatorID = SearchCreatorIdCheckBox.IsChecked == true;
        _settings.SearchParams.SearchExact = SearchExactCheckBox.IsChecked == true;
        _settings.HideViewerInfo = HideViewerInfoCheckBox.IsChecked == true;
        SaveDebugSettingsFromUi();
        _settings.UseProxy = UseProxyCheckBox.IsChecked == true;
        _settings.ApiLink = string.IsNullOrWhiteSpace(ApiLinkTextBox.Text)
            ? "https://api.bobac-analytics.com/smm1"
            : ApiLinkTextBox.Text.Trim();
        _settings.SelectedProfile = ProfileComboBox.SelectedItem?.ToString() ?? _settings.SelectedProfile;
        _settings.Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
    }

    private void SaveDebugSettingsFromUi()
    {
        _settings.Debug.LevelViewer = DebugLevelViewerCheckBox.IsChecked == true;
        _settings.Debug.TileRegions = DebugTileRegionsCheckBox.IsChecked == true;
        _settings.Debug.ShowGrid = ShowGridCheckBox.IsChecked == true;
    }

    private void ApplyLanguage()
    {
        VersionText.Text = $"{T("Version")}: {CurrentReleaseTag}";
        UpdateNotificationButtonText();
        NotificationHistoryTitle.Text = T("Notifications");
        ClearNotificationHistoryButton.Content = T("Clear");
        CloseNotificationHistoryButton.Content = T("Close");
        DownloadTab.Header = T("DownloadTab");
        SavedCoursesTab.Header = T("SavedCoursesTab");
        CemuTab.Header = T("CemuTab");
        SettingsTab.Header = T("SettingsTab");
        CreditsTab.Header = T("CreditsTab");

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
        _loadMoreSearchResultsItem.Text = T("LoadMore");
        SelectedCourseTitle.Text = T("SelectedCourse");
        SelectedLevelTitle.Text = T("NoCourseSelected");
        SelectedCreatorHeader.Text = T("Creator");
        SelectedWorldRecordHeader.Text = T("WorldRecord");
        BackFromProfileButton.Content = T("Back");
        BackFromCemuLevelButton.Content = T("Back");
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
        UnderworldPreviewButton.Content = GetAreaSwitchText(_selectedPreviewFile);
        HiddenBlocksPreviewButton.Content = CoursePreviewCanvas.ShowHiddenBlocks
            ? T("HideHiddenBlocks")
            : T("RevealHiddenBlocks");
        LevelViewerUnderworldButton.Content = GetAreaSwitchText(_largeViewerFile);
        LevelViewerUnderworldButton.IsVisible = _largeViewerLevel == null ||
                                                HasUnderworld(_largeViewerLevel, ResolveDownloadedCourseFolder);
        LevelViewerHiddenBlocksButton.Content = LevelViewerCanvas.ShowHiddenBlocks
            ? T("HideHiddenBlocks")
            : T("RevealHiddenBlocks");
        SavedCreatorHeader.Text = T("Creator");
        SavedClearRateHeader.Text = T("ClearRate");
        SavedWorldRecordHeader.Text = T("WorldRecord");
        CemuStatusText.Text = _cemuNodes.Count > 0 ? T("CemuSaveFound") : T("CemuSaveNotFound");
        RefreshCemuButton.Content = T("Refresh");
        OpenCemuFolderButton.Content = T("OpenFolder");
        CemuEmptyTitle.Text = T("CemuSaveNotFound");
        CemuEmptyMessage.Text = T("CemuSetupInstructions");
        CemuPageCreatorHeader.Text = T("Creator");
        CemuPageClearRateHeader.Text = T("ClearRate");
        CemuPageWorldRecordHeader.Text = T("WorldRecord");
        CemuPagePreviewInfo.Text = T("CoursePreviewHint");
        CemuPageUnderworldButton.Content = GetAreaSwitchText(_selectedCemuPreviewFile);
        CemuPageUnderworldButton.IsVisible = _selectedCemuNode?.Level == null ||
                                             HasUnderworld(_selectedCemuNode.Level, selected => selected.Folder);
        CemuPageHiddenBlocksButton.Content = CemuPagePreviewCanvas.ShowHiddenBlocks
            ? T("HideHiddenBlocks")
            : T("RevealHiddenBlocks");
        CemuReplaceTitle.Text = T("ReplaceWithDownloadedLevel");
        CemuReplaceSearchTextBox.Watermark = T("SearchDownloadedLevels");
        ReplaceCemuLevelButton.Content = T("Replace");

        SettingsTitle.Text = T("Settings");
        LanguageLabel.Text = T("Language");
        HideViewerInfoCheckBox.Content = T("HideViewerInfo");
        DebugSettingsTitle.Text = T("DebugMode");
        DebugLevelViewerTitle.Text = T("LevelViewer");
        DebugLevelViewerCheckBox.Content = T("DebugLevelViewer");
        DebugTileRegionsCheckBox.Content = T("DebugTileRegions");
        ShowGridCheckBox.Content = T("ShowGrid");
        UseProxyCheckBox.Content = T("UseProxy");
        OpenProxyFileButton.Content = T("OpenProxyFile");
        ApiEndpointLabel.Text = T("ApiEndpoint");
        SaveSettingsButton.Content = T("SaveSettings");
        ResetSettingsButton.Content = T("ResetSettings");
        RefreshAllDownloadedDataButton.Content = T("RefreshAllDownloadedData");
        CreditsTitle.Text = T("CreditsTitle");
        CreditsDescriptionText.Text = T("CreditsDescription");
        CreditsArchiveText.Text = T("CreditsArchive");
        CreditsDatabaseText.Text = T("CreditsDatabase");
        CreditsNintendoText.Text = T("CreditsNintendo");
        CreditsAboutTitle.Text = T("CreditsAbout");
        HerobrineRoleText.Text = T("ToolCreator");
        SupportTitle.Text = T("SupportTitle");
        SupportDescriptionText.Text = T("SupportDescription");
        SupportNoBenefitsText.Text = T("SupportNoBenefits");
        KofiButtonText.Text = T("OpenKofi");
        SnoozbusterCreditText.Text = T("CourseViewerCredit");
        LeoMauroCreditText.Text = T("CourseViewerCredit");
        SpecialThanksTitle.Text = T("SpecialThanksTitle");
        SpecialThanksText.Text = T("SpecialThanks");
        CreditsFeedbackText.Text = T("CreditsFeedback");
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
        _isLoadingCemuProfiles = true;
        try
        {
            ProfileComboBox.Items.Clear();
            foreach (var directory in _cemuSaveService.LoadProfiles(cemuPath))
            {
                ProfileComboBox.Items.Add(directory);
                if (directory == _settings.SelectedProfile)
                {
                    ProfileComboBox.SelectedItem = directory;
                }
            }

            if (ProfileComboBox.SelectedItem == null)
            {
                ProfileComboBox.SelectedItem = ProfileComboBox.Items.OfType<string>().FirstOrDefault();
            }
        }
        finally
        {
            _isLoadingCemuProfiles = false;
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

    private void RefreshCemuLevels()
    {
        var cemuDirectory = _cemuSaveService.ResolveCemuDirectory();
        LoadProfiles(cemuDirectory);

        var profilePath = GetCemuProfilePath(cemuDirectory);
        var levels = _cemuSaveService.LoadLevels(profilePath)
            .Select(EnrichCemuLevelWithSavedMetadata)
            .ToList();
        _cemuNodes.Clear();
        foreach (var level in levels)
        {
            _cemuNodes.Add(ToCemuNode(level));
        }

        UpdateCemuTabEmptyState(cemuDirectory);
        ResetCemuSelectedLevelDetails();
        Dispatcher.UIThread.Post(UpdateCemuCardWidth, DispatcherPriority.Background);
        SetStatus(_cemuNodes.Count == 0 ? T("CemuSaveNotFound") : $"Loaded {_cemuNodes.Count} CEMU level(s).");
    }

    private void UpdateCemuTabEmptyState(string? resolvedCemuDirectory)
    {
        var hasLevels = _cemuNodes.Count > 0;
        CemuLevelsPanel.IsVisible = hasLevels;
        CemuEmptyPanel.IsVisible = !hasLevels;
        var profilePath = GetCemuProfilePath(resolvedCemuDirectory);
        OpenCemuFolderButton.IsEnabled = !string.IsNullOrWhiteSpace(profilePath) &&
                                         Directory.Exists(profilePath);
        CemuStatusText.Text = hasLevels ? T("CemuSaveFound") : T("CemuSaveNotFound");

        if (hasLevels)
        {
            return;
        }

        CemuEmptyTitle.Text = T("CemuSaveNotFound");
        CemuEmptyMessage.Text = T("CemuSetupInstructions");
        CemuEmptyPathText.Text = string.IsNullOrWhiteSpace(resolvedCemuDirectory)
            ? T("CemuDetectedPathNotFound")
            : string.Format(CultureInfo.InvariantCulture, T("CemuDetectedPath"), resolvedCemuDirectory);
    }

    private SavedLevelNode ToCemuNode(LevelInfo level)
    {
        return new SavedLevelNode
        {
            DisplayName = string.IsNullOrWhiteSpace(level.Name) ? Path.GetFileName(level.Folder) ?? "CEMU Level" : level.Name,
            Summary = level.Folder,
            ShortInfo = Path.GetFileName(level.Folder) ?? level.Folder,
            Thumbnail = _thumbnailLoader.LoadCourseThumbnail(level.Folder),
            RowMargin = new Thickness(0, 5, 0, 5),
            Level = level
        };
    }

    private void UpdateCemuCardWidth()
    {
        var wrapPanel = CemuLevelsItemsControl.GetVisualDescendants().OfType<WrapPanel>().FirstOrDefault();
        if (wrapPanel == null)
        {
            return;
        }

        var availableWidth = Math.Max(174, CemuLevelsScrollViewer.Viewport.Width - 8);
        if (double.IsNaN(availableWidth) || availableWidth <= 0)
        {
            availableWidth = Math.Max(174, CemuLevelsScrollViewer.Bounds.Width - 8);
        }

        const double minCardWidth = 174;
        const double horizontalGap = 12;
        var columns = Math.Max(1, (int)Math.Floor((availableWidth + horizontalGap) / (minCardWidth + horizontalGap)));
        var cardWidth = Math.Max(minCardWidth, Math.Floor((availableWidth - (columns * horizontalGap)) / columns));
        wrapPanel.ItemWidth = cardWidth;
    }

    private LevelInfo EnrichCemuLevelWithSavedMetadata(LevelInfo cemuLevel)
    {
        var cemuFolder = cemuLevel.Folder;
        var savedLevel = FindSavedMetadataForCemuLevel(cemuLevel);
        if (savedLevel == null)
        {
            return cemuLevel;
        }

        return new LevelInfo
        {
            Url = savedLevel.Url,
            Name = string.IsNullOrWhiteSpace(savedLevel.Name) ? cemuLevel.Name : savedLevel.Name,
            Creator = savedLevel.Creator,
            CreatorMiiData = savedLevel.CreatorMiiData,
            LevelId = savedLevel.LevelId,
            CreatorId = savedLevel.CreatorId,
            Clears = savedLevel.Clears,
            Failures = savedLevel.Failures,
            TotalAttempts = savedLevel.TotalAttempts,
            ClearRate = savedLevel.ClearRate,
            UploadTime = savedLevel.UploadTime,
            WorldRecordMs = savedLevel.WorldRecordMs,
            WorldRecordHolderNnid = savedLevel.WorldRecordHolderNnid,
            WorldRecordBestTimePlayerMiiData = savedLevel.WorldRecordBestTimePlayerMiiData,
            Stars = savedLevel.Stars,
            Downloads = savedLevel.Downloads,
            Pack = savedLevel.Pack,
            Folder = cemuFolder,
            Thumbnail = _thumbnailLoader.LoadCourseThumbnail(cemuFolder)
        };
    }

    private LevelInfo? FindSavedMetadataForCemuLevel(LevelInfo cemuLevel)
    {
        var savedLevels = JsonStore.ToLevelList(_store.LoadDownloaded())
            .Concat(JsonStore.ToLevelList(LoadLevelFile(_paths.BackuppedFile)))
            .ToList();

        if (cemuLevel.LevelId > 0)
        {
            var idMatch = savedLevels.FirstOrDefault(level => level.LevelId == cemuLevel.LevelId);
            if (idMatch != null)
            {
                return idMatch;
            }
        }

        return savedLevels.FirstOrDefault(level =>
            !string.IsNullOrWhiteSpace(level.Name) &&
            string.Equals(level.Name, cemuLevel.Name, StringComparison.OrdinalIgnoreCase));
    }

    private void AddSearchResult(LevelInfo level)
    {
        MarkDownloadState(level);
        var loadMoreIndex = _searchResults.IndexOf(_loadMoreSearchResultsItem);
        if (loadMoreIndex >= 0)
        {
            _searchResults.Insert(loadMoreIndex, level);
        }
        else
        {
            _searchResults.Add(level);
        }

        _ = LoadSearchResultMiiImagesAsync(level);
    }

    private IEnumerable<LevelInfo> SearchResultLevels => _searchResults.OfType<LevelInfo>();

    private int SearchResultCount => _searchResults.OfType<LevelInfo>().Count();

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

    private async Task LoadCreditImagesAsync(CancellationToken cancellationToken)
    {
        await LoadCreditImageAsync(HerobrineAvatarImage, "https://github.com/HerobrineTV.png?size=128", cancellationToken);
        await LoadCreditImageAsync(SnoozbusterAvatarImage, "https://github.com/snoozbuster.png?size=128", cancellationToken);
        await LoadCreditImageAsync(LeoMauroAvatarImage, "https://github.com/leomaurodesenv.png?size=128", cancellationToken);
    }

    private async Task LoadCreditImageAsync(Image image, string url, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _statusHttpClient.GetByteArrayAsync(url, cancellationToken);
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            await Dispatcher.UIThread.InvokeAsync(() => image.Source = bitmap);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
        }
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
        foreach (var result in SearchResultLevels.Where(result => result.LevelId == levelId))
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
            .Concat(LoadLevelBackupsNode())
            .Concat(LoadLevelsFromLevelPacks())
            .OrderBy(node => node.IsFolder ? 0 : 1)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<SavedLevelNode> LoadLevelBackupsNode()
    {
        var root = Path.Combine(_paths.BackuppedDirectory, LevelBackupsFolderName);
        var children = new ObservableCollection<SavedLevelNode>();
        if (Directory.Exists(root))
        {
            foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName))
            {
                var level = new LevelInfo
                {
                    LevelId = TryReadLevelIdFromFolder(directory),
                    Name = GetDisplayNameFromCourseFolder(directory),
                    Creator = "Backup",
                    Folder = directory
                };
                children.Add(new SavedLevelNode
                {
                    DisplayName = level.Name,
                    Summary = level.Summary,
                    ShortInfo = BuildSavedLevelShortInfo(level),
                    Thumbnail = _thumbnailLoader.LoadCourseThumbnail(directory),
                    RowMargin = new Thickness(0, 5, 0, 5),
                    Level = level
                });
            }
        }

        if (children.Count == 0)
        {
            return [];
        }

        return
        [
            new SavedLevelNode
            {
                DisplayName = LevelBackupsFolderName,
                Summary = $"{children.Count} backup(s)",
                ShortInfo = $"{children.Count} backup(s)",
                IsFolder = true,
                IsProtectedFolder = true,
                RowMargin = new Thickness(-18, 5, 0, 5),
                Children = children
            }
        ];
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
            UnderworldPreviewButton.Content = GetAreaSwitchText(courseFileName);
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

    private bool HasUnderworld(LevelInfo level, Func<LevelInfo, string?> resolveFolder)
    {
        var folder = resolveFolder(level);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        var underworldFile = Path.Combine(folder, "course_data_sub.cdt");
        if (!File.Exists(underworldFile))
        {
            return false;
        }

        try
        {
            var preview = _courseParser.Read(underworldFile);
            return preview.ObjectCount > 0;
        }
        catch
        {
            return false;
        }
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
        LevelViewerTitle.Text = $"{level.Name} | {level.DisplayCode} | DL {level.DownloadsText}";
        LevelViewerCanvas.ShowHiddenBlocks = false;
        LevelViewerHiddenBlocksButton.Content = T("RevealHiddenBlocks");
        UpdateLargeViewerAreaButtons(level);
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
            LevelViewerUnderworldButton.Content = GetAreaSwitchText(courseFileName);
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

    private void ResetCemuSelectedLevelDetails()
    {
        _selectedCemuNode = null;
        _selectedCemuReplacementLevel = null;
        CemuLevelPage.IsVisible = false;
        CemuPagePreviewCanvas.Course = null;
        CemuReplaceResultsListBox.SelectedItem = null;
        _cemuReplaceResults.Clear();
        UpdateReplaceCemuLevelButtonState();
    }

    private void OpenCemuLevelPage(LevelInfo level)
    {
        MainTabs.IsVisible = false;
        CemuLevelPage.IsVisible = true;
        CemuPageTitle.Text = level.LevelId > 0 ? $"{level.Name} | {level.DisplayCode}" : level.Name;
        CemuPageCreatorText.Text = string.IsNullOrWhiteSpace(level.Creator) ? "CEMU" : level.Creator;
        CemuPageClearRateText.Text = level.TotalAttempts > 0
            ? $"{level.ClearRate * 100:0.##}% ({level.Clears}/{level.TotalAttempts})"
            : "n/a";
        CemuPageWorldRecordText.Text = level.WorldRecordMs > 0
            ? $"{FormatTime(level.WorldRecordMs)} by {FormatName(level.WorldRecordHolderNnid)}"
            : "n/a";
        CemuPageStarsText.Text = $"★ Stars: {level.StarsText}";
        CemuPageDownloadsText.Text = $"DL Downloads: {level.DownloadsText}";
        CemuPageTotalRunsText.Text = $"▶ Total Runs: {level.TotalAttemptsText}";
        CemuPageClearsText.Text = $"✓ Clears: {level.ClearsText}";
        CemuPageFolderText.Text = Path.GetFileName(level.Folder) ?? level.Folder;
        CemuLevelHeaderThumbnail.Source = _thumbnailLoader.LoadCourseThumbnail(level.Folder);
        CemuCreatorMiiImage.Source = null;
        CemuCreatorMiiBorder.IsVisible = false;
        CemuWorldRecordMiiImage.Source = null;
        CemuWorldRecordMiiBorder.IsVisible = false;
        _ = LoadCemuLevelMiiImagesAsync(level);

        CemuPagePreviewCanvas.ShowHiddenBlocks = false;
        CemuPageHiddenBlocksButton.Content = T("RevealHiddenBlocks");
        _selectedCemuPreviewFile = "course_data.cdt";
        UpdateCemuPageAreaButtons(level);
        LoadCemuPagePreview(level, _selectedCemuPreviewFile);
        UpdateReplaceCemuLevelButtonState();
    }

    private async Task LoadCemuLevelMiiImagesAsync(LevelInfo level)
    {
        var creator = await _miiImageLoader.LoadAsync(level.CreatorMiiData, CancellationToken.None);
        if (_selectedCemuNode?.Level != level)
        {
            return;
        }

        level.CreatorMiiImage = creator;
        CemuCreatorMiiImage.Source = creator;
        CemuCreatorMiiBorder.IsVisible = creator != null;

        var worldRecord = await _miiImageLoader.LoadAsync(level.WorldRecordBestTimePlayerMiiData, CancellationToken.None);
        if (_selectedCemuNode?.Level != level)
        {
            return;
        }

        level.WorldRecordMiiImage = worldRecord;
        CemuWorldRecordMiiImage.Source = worldRecord;
        CemuWorldRecordMiiBorder.IsVisible = worldRecord != null;
    }

    private void LoadCemuPagePreview(LevelInfo level, string courseFileName)
    {
        try
        {
            var courseData = Path.Combine(level.Folder, courseFileName);
            if (!File.Exists(courseData))
            {
                CemuPagePreviewCanvas.Course = null;
                CemuPagePreviewInfo.Text = $"No {courseFileName} found for this entry.";
                return;
            }

            var preview = _courseParser.Read(courseData);
            CemuPagePreviewCanvas.Course = preview;
            var areaName = courseFileName == "course_data_sub.cdt" ? T("Underworld") : T("Overworld");
            CemuPagePreviewInfo.Text = $"{areaName} - {preview.Summary}";
            CemuPageUnderworldButton.Content = GetAreaSwitchText(courseFileName);
            Dispatcher.UIThread.Post(() =>
            {
                var maxY = Math.Max(0, CemuPagePreviewScrollViewer.Extent.Height - CemuPagePreviewScrollViewer.Viewport.Height);
                CemuPagePreviewScrollViewer.Offset = new Vector(0, maxY);
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            CemuPagePreviewCanvas.Course = null;
            CemuPagePreviewInfo.Text = $"Preview could not be loaded: {ex.Message}";
        }
    }

    private void LoadCemuReplaceResults(string filter)
    {
        var levels = JsonStore.ToLevelList(_store.LoadDownloaded())
            .Where(level => string.IsNullOrWhiteSpace(level.Pack))
            .Concat(LoadLevelPackReplacementLevels())
            .Where(level => MatchesFilter(level, filter))
            .OrderBy(level => level.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _cemuReplaceResults.Clear();
        foreach (var level in levels)
        {
            LoadDownloadedThumbnail(level);
            _cemuReplaceResults.Add(level);
        }
    }

    private IEnumerable<LevelInfo> LoadLevelPackReplacementLevels()
    {
        var packs = _store.LoadLevelPacks();
        foreach (var level in _store.LoadDownloaded().Values.Where(level => !string.IsNullOrWhiteSpace(level.Pack)))
        {
            level.Folder = ResolveCourseFolder(level);
            if (Directory.Exists(level.Folder))
            {
                yield return level;
            }
        }

        foreach (var (packName, packFolder) in packs)
        {
            var packRoot = Path.Combine(_paths.LevelPacksDirectory, packFolder);
            if (!Directory.Exists(packRoot))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(packRoot))
            {
                if (!File.Exists(Path.Combine(directory, "course_data.cdt")))
                {
                    continue;
                }

                var levelId = TryReadLevelIdFromFolder(directory);
                if (_store.LoadDownloaded().Values.Any(level =>
                        level.LevelId == levelId &&
                        string.Equals(level.Pack, packName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                yield return new LevelInfo
                {
                    LevelId = levelId,
                    Name = GetDisplayNameFromCourseFolder(directory),
                    Creator = "Local",
                    Pack = packName,
                    Folder = directory
                };
            }
        }
    }

    private void UpdateReplaceCemuLevelButtonState()
    {
        ReplaceCemuLevelButton.IsEnabled = _selectedCemuNode?.Level != null && _selectedCemuReplacementLevel != null;
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
                    IsProtectedFolder = node.IsProtectedFolder,
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

    private string GetCemuProfilePath(string? cemuDirectory = null)
    {
        var profile = ProfileComboBox.SelectedItem?.ToString() ?? _settings.SelectedProfile;
        return _cemuSaveService.GetProfilePath(cemuDirectory ?? _cemuSaveService.ResolveCemuDirectory(), profile);
    }

    private string? GetPackFolderIfEnabled()
    {
        if (DownloadToPackCheckBox.IsChecked != true)
        {
            return null;
        }

        var packName = string.IsNullOrWhiteSpace(PackNameTextBox.Text) ? "Default Pack" : PackNameTextBox.Text.Trim();
        if (string.Equals(packName, LevelBackupsFolderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{LevelBackupsFolderName} is reserved for CEMU backups.");
        }

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
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SetStatus(message));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        _notifications.Insert(0, new NotificationEntry
        {
            Timestamp = timestamp,
            Message = message
        });
        NotificationToastText.Text = message;
        NotificationToast.IsVisible = !NotificationHistoryPanel.IsVisible;
        UpdateNotificationButtonText();

        var sequence = ++_notificationSequence;
        _ = HideNotificationToastLaterAsync(sequence);
    }

    private async Task HideNotificationToastLaterAsync(int sequence)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), _lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (sequence == _notificationSequence)
            {
                NotificationToast.IsVisible = false;
            }
        });
    }

    private void UpdateNotificationButtonText()
    {
        NotificationButton.Content = $"{T("Notifications")} ({_notifications.Count})";
    }

    private void ApplyViewerInfoVisibility()
    {
        CoursePreviewInfo.IsVisible = !_settings.HideViewerInfo;
        LevelViewerInfo.IsVisible = !_settings.HideViewerInfo;
    }

    private void ApplyDebugSettings()
    {
        var debugLevelViewer = _settings.Debug.LevelViewer;
        var debugTileRegions = _settings.Debug.TileRegions;
        var showGrid = _settings.Debug.ShowGrid;
        SearchSelectedPreviewCanvas.DebugLevelViewer = debugLevelViewer;
        SearchSelectedPreviewCanvas.DebugTileRegions = debugTileRegions;
        SearchSelectedPreviewCanvas.ShowGrid = showGrid;
        CoursePreviewCanvas.DebugLevelViewer = debugLevelViewer;
        CoursePreviewCanvas.DebugTileRegions = debugTileRegions;
        CoursePreviewCanvas.ShowGrid = showGrid;
        ProfileSelectedPreviewCanvas.DebugLevelViewer = debugLevelViewer;
        ProfileSelectedPreviewCanvas.DebugTileRegions = debugTileRegions;
        ProfileSelectedPreviewCanvas.ShowGrid = showGrid;
        CemuPagePreviewCanvas.DebugLevelViewer = debugLevelViewer;
        CemuPagePreviewCanvas.DebugTileRegions = debugTileRegions;
        CemuPagePreviewCanvas.ShowGrid = showGrid;
        LevelViewerCanvas.DebugLevelViewer = debugLevelViewer;
        LevelViewerCanvas.DebugTileRegions = debugTileRegions;
        LevelViewerCanvas.ShowGrid = showGrid;
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
        ["CemuTab"] = "CEMU",
        ["SettingsTab"] = "Settings",
        ["CreditsTab"] = "Credits",
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
        ["LoadMore"] = "Load More",
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
        ["Replace"] = "Replace",
        ["Cancel"] = "Cancel",
        ["Close"] = "Close",
        ["Clear"] = "Clear",
        ["Ok"] = "OK",
        ["Notifications"] = "Notifications",
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
        ["CemuSaveFound"] = "CEMU save found",
        ["CemuSaveNotFound"] = "CEMU save was not found",
        ["CemuSetupInstructions"] = "CEMU is detected automatically when a folder containing Cemu.exe and the mlc01 directory is found. The save is expected below mlc01/usr/save/00050000/<Super Mario Maker title id>/user/<profile>.",
        ["CemuDetectedPath"] = "Detected path: {0}",
        ["CemuDetectedPathNotFound"] = "Detected path: not found",
        ["NoCemuLevelSelected"] = "No CEMU level selected",
        ["ReplaceWithDownloadedLevel"] = "Replace with downloaded level",
        ["SearchDownloadedLevels"] = "Search downloaded levels",
        ["ReplaceCemuLevelTitle"] = "Replace CEMU Level",
        ["ReplaceCemuLevelWarning"] = "Replace '{0}' in your CEMU save with '{1}'?\n\nA backup of the current CEMU level folder will be created first.",
        ["Settings"] = "Settings",
        ["Language"] = "Language",
        ["HideViewerInfo"] = "Hide Viewer Info",
        ["DebugMode"] = "Debug Mode",
        ["LevelViewer"] = "Level Viewer",
        ["DebugLevelViewer"] = "Debug Tiles",
        ["DebugTileRegions"] = "Debug Tile Regions",
        ["ShowGrid"] = "Show Grid",
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
        ["Version"] = "Version",
        ["SettingsSaved"] = "Settings saved.",
        ["SettingsReset"] = "Settings reset.",
        ["CreditsTitle"] = "Information",
        ["CreditsDescription"] = "This is a fanmade level downloader for Super Mario Maker 1.",
        ["CreditsArchive"] = "It uses saved levels from the Wayback Machine (archive.org).",
        ["CreditsDatabase"] = "The level list is indexed in a database so courses can be searched.",
        ["CreditsNintendo"] = "Super Mario Maker, Mario and all related Nintendo assets belong to Nintendo.",
        ["CreditsAbout"] = "Credits",
        ["ToolCreator"] = "Creator of this Tool",
        ["SupportTitle"] = "Support this project",
        ["SupportDescription"] = "If you want to help with server costs or development progress, you can support the project on Ko-fi.",
        ["SupportNoBenefits"] = "Support is completely optional and does not unlock extra benefits.",
        ["OpenKofi"] = "Open Ko-fi",
        ["CourseViewerCredit"] = "Course Viewer for the Course Display",
        ["SpecialThanksTitle"] = "Special Thanks",
        ["SpecialThanks"] = "Special thanks to James M***, who brought the project back into focus and made this rework finally get started.",
        ["CreditsFeedback"] = "Leave feedback or requests for help at any time.",
        ["PrereleaseWarningTitle"] = "Prerelease Version",
        ["PrereleaseWarningMessage"] = "This is a prerelease version. Bugs can occur. If you find bugs, please contact me on Discord: nintendo_switch.",
        ["DontShowAgain"] = "Do not show again"
    };

    private static readonly Dictionary<string, string> GermanText = new()
    {
        ["DownloadTab"] = "Download",
        ["SavedCoursesTab"] = "Gespeicherte Level",
        ["CemuTab"] = "CEMU",
        ["SettingsTab"] = "Einstellungen",
        ["CreditsTab"] = "Credits",
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
        ["LoadMore"] = "Mehr laden",
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
        ["Replace"] = "Ersetzen",
        ["Cancel"] = "Abbrechen",
        ["Close"] = "Schliessen",
        ["Clear"] = "Leeren",
        ["Ok"] = "OK",
        ["Notifications"] = "Meldungen",
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
        ["CemuSaveFound"] = "CEMU-Save gefunden",
        ["CemuSaveNotFound"] = "CEMU-Save wurde nicht gefunden",
        ["CemuSetupInstructions"] = "CEMU wird automatisch erkannt, wenn ein Ordner mit Cemu.exe und dem mlc01-Ordner gefunden wird. Der Save wird unter mlc01/usr/save/00050000/<Super Mario Maker Title-ID>/user/<Profil> erwartet.",
        ["CemuDetectedPath"] = "Erkannter Pfad: {0}",
        ["CemuDetectedPathNotFound"] = "Erkannter Pfad: nicht gefunden",
        ["NoCemuLevelSelected"] = "Kein CEMU-Level ausgewaehlt",
        ["ReplaceWithDownloadedLevel"] = "Mit geladenem Level ersetzen",
        ["SearchDownloadedLevels"] = "Geladene Level suchen",
        ["ReplaceCemuLevelTitle"] = "CEMU-Level ersetzen",
        ["ReplaceCemuLevelWarning"] = "'{0}' im CEMU-Save durch '{1}' ersetzen?\n\nVorher wird ein Backup des aktuellen CEMU-Level-Ordners erstellt.",
        ["Settings"] = "Einstellungen",
        ["Language"] = "Sprache",
        ["HideViewerInfo"] = "Viewer-Info ausblenden",
        ["DebugMode"] = "Debug-Modus",
        ["LevelViewer"] = "Level Viewer",
        ["DebugLevelViewer"] = "Tiles debuggen",
        ["DebugTileRegions"] = "Tile-Regionen debuggen",
        ["ShowGrid"] = "Raster anzeigen",
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
        ["Version"] = "Version",
        ["SettingsSaved"] = "Einstellungen gespeichert.",
        ["SettingsReset"] = "Einstellungen zurueckgesetzt.",
        ["CreditsTitle"] = "Information",
        ["CreditsDescription"] = "Dies ist ein fanmade Level-Downloader fuer Super Mario Maker 1.",
        ["CreditsArchive"] = "Er nutzt gespeicherte Level aus der Wayback Machine (archive.org).",
        ["CreditsDatabase"] = "Die Levelliste ist in einer Datenbank indexiert, damit Level gesucht werden koennen.",
        ["CreditsNintendo"] = "Super Mario Maker, Mario und alle zugehoerigen Nintendo-Assets gehoeren Nintendo.",
        ["CreditsAbout"] = "Credits",
        ["ToolCreator"] = "Creator of this Tool",
        ["SupportTitle"] = "Projekt unterstuetzen",
        ["SupportDescription"] = "Wenn du bei Serverkosten oder Development Progress helfen moechtest, kannst du das Projekt auf Ko-fi unterstuetzen.",
        ["SupportNoBenefits"] = "Support ist komplett freiwillig und schaltet keine Extra-Benefits frei.",
        ["OpenKofi"] = "Ko-fi oeffnen",
        ["CourseViewerCredit"] = "Course Viewer fuer die Level-Anzeige",
        ["SpecialThanksTitle"] = "Special Thanks",
        ["SpecialThanks"] = "Special Thanks an James M***, der das Projekt wieder in den Fokus gerueckt hat und wodurch dieser Rework endlich losging.",
        ["CreditsFeedback"] = "Feedback oder Hilfeanfragen sind jederzeit willkommen.",
        ["PrereleaseWarningTitle"] = "Prerelease-Version",
        ["PrereleaseWarningMessage"] = "Dies ist eine Prerelease-Version. Es koennen Bugs auftreten. Wenn du Bugs findest, melde dich gerne bei mir auf Discord: nintendo_switch.",
        ["DontShowAgain"] = "Nicht erneut anzeigen"
    };

    private sealed class DownloadState
    {
        public bool IsDownloading { get; set; }
        public bool IsDownloaded { get; set; }
        public string ProgressText { get; set; } = "";
    }
}

public sealed class LoadMoreSearchResultsItem : INotifyPropertyChanged
{
    private string _text = "";
    private bool _isEnabled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            OnPropertyChanged(nameof(Text));
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
