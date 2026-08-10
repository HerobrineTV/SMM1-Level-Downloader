namespace SMMDownloader.Avalonia.Services;

public sealed class ProjectPaths
{
    public ProjectPaths()
    {
        RootDirectory = FindRepositoryRoot();
        LegacyDataDirectory = Path.Combine(RootDirectory, "SMMDownloader", "Data");
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SMM1-Level-Downloader",
            "Data");
        DownloadCacheDirectory = Path.Combine(DataDirectory, "DownloadCache");
        LevelPacksDirectory = Path.Combine(DataDirectory, "LevelPacks");
        OfficialCoursesDirectory = Path.Combine(DataDirectory, "OfficialCourses");
        BackuppedDirectory = Path.Combine(DataDirectory, "Backupped");
        SettingsFile = Path.Combine(DataDirectory, "data.json");
        DownloadedFile = Path.Combine(DataDirectory, "downloaded.json");
        BackuppedFile = Path.Combine(DataDirectory, "backupped.json");
        LevelPacksFile = Path.Combine(DataDirectory, "levelpacks.json");
        ProxyFile = Path.Combine(DataDirectory, "proxies.txt");
        SoundFile = Path.Combine(DataDirectory, "sound.bwv");
        MigrationMarkerFile = Path.Combine(DataDirectory, ".legacy-data-migrated");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(DownloadCacheDirectory);
        Directory.CreateDirectory(LevelPacksDirectory);
    }

    public string RootDirectory { get; }
    public string LegacyDataDirectory { get; }
    public string DataDirectory { get; }
    public string DownloadCacheDirectory { get; }
    public string LevelPacksDirectory { get; }
    public string OfficialCoursesDirectory { get; }
    public string BackuppedDirectory { get; }
    public string SettingsFile { get; }
    public string DownloadedFile { get; }
    public string BackuppedFile { get; }
    public string LevelPacksFile { get; }
    public string ProxyFile { get; }
    public string SoundFile { get; }
    public string MigrationMarkerFile { get; }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if ((File.Exists(Path.Combine(directory, "SMM1-Level-Downloader.sln")) ||
                 File.Exists(Path.Combine(directory, "SMM1-Level-Downloader.slnx"))) &&
                File.Exists(Path.Combine(directory, "AvaloniaApp", "SMMDownloader.Avalonia.csproj")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
