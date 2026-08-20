using Avalonia.Platform;

namespace SMMDownloader.Avalonia.Services;

public sealed class StandardSoundService(ProjectPaths paths, CemuSaveService cemuSaveService)
{
    private const string SoundFileName = "sound.bwv";
    private static readonly Uri SoundAssetUri = new("avares://SMMDownloader.Avalonia/Assets/sound.bwv");

    public void EnsureStandardSoundFile()
    {
        if (File.Exists(paths.SoundFile))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(paths.SoundFile)!);
        using var source = AssetLoader.Open(SoundAssetUri);
        using var target = File.Create(paths.SoundFile);
        source.CopyTo(target);
    }

    public int EnsureExistingCourseSounds(string? cemuDirectory)
    {
        EnsureStandardSoundFile();

        var updated = 0;
        foreach (var folder in EnumerateKnownCourseFolders(cemuDirectory))
        {
            updated += EnsureCourseSoundFile(folder) ? 1 : 0;
        }

        return updated;
    }

    public bool EnsureCourseSoundFile(string courseFolder)
    {
        if (string.IsNullOrWhiteSpace(courseFolder) ||
            !Directory.Exists(courseFolder) ||
            !File.Exists(Path.Combine(courseFolder, "course_data.cdt")))
        {
            return false;
        }

        var target = Path.Combine(courseFolder, SoundFileName);
        if (File.Exists(target))
        {
            return false;
        }

        EnsureStandardSoundFile();
        File.Copy(paths.SoundFile, target);
        return true;
    }

    private IEnumerable<string> EnumerateKnownCourseFolders(string? cemuDirectory)
    {
        foreach (var folder in EnumerateCourseFolders(paths.DownloadCacheDirectory))
        {
            yield return folder;
        }

        foreach (var folder in EnumerateCourseFolders(paths.LevelPacksDirectory))
        {
            yield return folder;
        }

        foreach (var folder in EnumerateCourseFolders(Path.Combine(paths.OfficialCoursesDirectory, "CourseFiles")))
        {
            yield return folder;
        }

        foreach (var folder in EnumerateCourseFolders(paths.BackuppedDirectory))
        {
            yield return folder;
        }

        foreach (var profile in cemuSaveService.LoadProfiles(cemuDirectory))
        {
            var profilePath = cemuSaveService.GetProfilePath(cemuDirectory, profile);
            foreach (var folder in cemuSaveService.LoadLevels(profilePath).Select(level => level.Folder))
            {
                yield return folder;
            }
        }
    }

    private static IEnumerable<string> EnumerateCourseFolders(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (File.Exists(Path.Combine(directory, "course_data.cdt")))
            {
                yield return directory;
            }
        }
    }
}
