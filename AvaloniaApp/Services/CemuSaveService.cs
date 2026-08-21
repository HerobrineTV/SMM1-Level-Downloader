using SMMDownloader.Avalonia.Models;

namespace SMMDownloader.Avalonia.Services;

public sealed class CemuSaveService(ProjectPaths paths, SmmCourseParser courseParser)
{
    private static readonly string[] CourseFileNames =
    [
        "course_data.cdt",
        "course_data_sub.cdt",
        "thumbnail0.tnl",
        "thumbnail1.tnl",
        "sound.bwv"
    ];
    private static readonly string[] SmmTitleIds = ["1018dd00", "1018dc00", "1018db00"];

    public string? ResolveCemuDirectory()
    {
        return GetCandidateDirectories()
            .Where(IsCemuDirectory)
            .Select(Path.GetFullPath)
            .FirstOrDefault();
    }

    public IReadOnlyList<string> LoadProfiles(string? cemuDirectory)
    {
        var userRoot = GetUserSaveRoot(cemuDirectory);
        if (string.IsNullOrWhiteSpace(userRoot) || !Directory.Exists(userRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(userRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           !string.Equals(name, "common", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    public string GetProfilePath(string? cemuDirectory, string? profile)
    {
        return string.IsNullOrWhiteSpace(cemuDirectory) || string.IsNullOrWhiteSpace(profile)
            ? ""
            : Path.Combine(GetUserSaveRoot(cemuDirectory) ?? "", profile);
    }

    public IReadOnlyList<LevelInfo> LoadLevels(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath) || !Directory.Exists(profilePath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(profilePath)
            .Where(directory => File.Exists(Path.Combine(directory, "course_data.cdt")))
            .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
            .Select(CreateLevel)
            .ToList();
    }

    public string BackupAndReplace(LevelInfo cemuLevel, LevelInfo replacementLevel, string replacementFolder)
    {
        if (string.IsNullOrWhiteSpace(cemuLevel.Folder) || !Directory.Exists(cemuLevel.Folder))
        {
            throw new InvalidOperationException("Selected CEMU level folder does not exist.");
        }

        if (string.IsNullOrWhiteSpace(replacementFolder) || !Directory.Exists(replacementFolder))
        {
            throw new InvalidOperationException("Selected replacement level folder does not exist.");
        }

        if (!File.Exists(Path.Combine(replacementFolder, "course_data.cdt")))
        {
            throw new InvalidOperationException("Selected replacement has no course_data.cdt.");
        }

        var backupRoot = Path.Combine(paths.BackuppedDirectory, "LevelBackups");
        Directory.CreateDirectory(backupRoot);
        var backupFolder = Path.Combine(
            backupRoot,
            $"{DateTime.Now:yyyyMMdd_HHmmss}_{SanitizeFolderName(cemuLevel.Name)}");
        CopyDirectory(cemuLevel.Folder, backupFolder);

        foreach (var fileName in CourseFileNames)
        {
            var source = Path.Combine(replacementFolder, fileName);
            var target = Path.Combine(cemuLevel.Folder, fileName);
            if (File.Exists(source))
            {
                File.Copy(source, target, true);
            }
            else if (File.Exists(target))
            {
                File.Delete(target);
            }
        }

        return backupFolder;
    }

    private LevelInfo CreateLevel(string directory)
    {
        var levelName = Path.GetFileName(directory) ?? directory;
        try
        {
            var preview = courseParser.Read(Path.Combine(directory, "course_data.cdt"));
            if (!string.IsNullOrWhiteSpace(preview.Name))
            {
                levelName = preview.Name;
            }
        }
        catch
        {
        }

        return new LevelInfo
        {
            LevelId = TryReadLevelIdFromFolder(directory),
            Name = levelName,
            Creator = "CEMU",
            Folder = directory
        };
    }

    private static string? GetUserSaveRoot(string? cemuDirectory)
    {
        if (string.IsNullOrWhiteSpace(cemuDirectory))
        {
            return null;
        }

        var titleRoot = Path.Combine(cemuDirectory, "mlc01", "usr", "save", "00050000");
        foreach (var titleId in SmmTitleIds)
        {
            var userRoot = Path.Combine(titleRoot, titleId, "user");
            if (Directory.Exists(userRoot))
            {
                return userRoot;
            }
        }

        return Path.Combine(titleRoot, SmmTitleIds[0], "user");
    }

    private static bool IsCemuDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        return File.Exists(Path.Combine(path, "Cemu.exe")) ||
               File.Exists(Path.Combine(path, "cemu.exe")) ||
               SmmTitleIds.Any(titleId =>
                   Directory.Exists(Path.Combine(path, "mlc01", "usr", "save", "00050000", titleId, "user")));
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        var candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        };

        foreach (var root in candidates.Where(directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)))
        {
            yield return Path.Combine(root, "Cemu");

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(root, "Cemu*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var child in children)
            {
                yield return child;
            }
        }
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

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(sourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static string SanitizeFolderName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = value
            .Trim()
            .Select(character => invalid.Contains(character) ? '_' : character)
            .Select(character => char.IsControl(character) ? '_' : character);
        var sanitized = string.Concat(chars).Trim(' ', '.', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Cemu_Level" : sanitized;
    }
}
