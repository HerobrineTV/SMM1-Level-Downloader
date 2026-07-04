namespace SMMDownloader.Avalonia.Services;

public sealed class DataMigrationService(ProjectPaths paths)
{
    public bool NeedsLegacyDataMigration()
    {
        if (File.Exists(paths.MigrationMarkerFile))
        {
            return false;
        }

        if (Path.GetFullPath(paths.LegacyDataDirectory)
            .Equals(Path.GetFullPath(paths.DataDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Directory.Exists(paths.LegacyDataDirectory) &&
               Directory.EnumerateFileSystemEntries(paths.LegacyDataDirectory).Any();
    }

    public Task MigrateLegacyDataAsync(CancellationToken token)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(paths.DataDirectory);
            MoveDirectoryContents(paths.LegacyDataDirectory, paths.DataDirectory, token);

            File.WriteAllText(
                paths.MigrationMarkerFile,
                $"Migrated from: {paths.LegacyDataDirectory}{Environment.NewLine}" +
                $"Migrated to: {paths.DataDirectory}{Environment.NewLine}" +
                $"Migrated at: {DateTimeOffset.Now:O}{Environment.NewLine}");
        }, token);
    }

    private static void MoveDirectoryContents(string sourceDirectory, string targetDirectory, CancellationToken token)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory))
        {
            token.ThrowIfCancellationRequested();
            var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
            MoveFile(sourceFile, targetFile);
        }

        foreach (var sourceChildDirectory in Directory.EnumerateDirectories(sourceDirectory))
        {
            token.ThrowIfCancellationRequested();
            var targetChildDirectory = Path.Combine(targetDirectory, Path.GetFileName(sourceChildDirectory));
            MoveDirectoryContents(sourceChildDirectory, targetChildDirectory, token);

            if (!Directory.EnumerateFileSystemEntries(sourceChildDirectory).Any())
            {
                Directory.Delete(sourceChildDirectory);
            }
        }
    }

    private static void MoveFile(string sourceFile, string targetFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

        if (!File.Exists(targetFile))
        {
            File.Move(sourceFile, targetFile);
            return;
        }

        if (FilesAreEqual(sourceFile, targetFile))
        {
            File.Delete(sourceFile);
            return;
        }

        File.Move(sourceFile, CreateConflictPath(targetFile));
    }

    private static bool FilesAreEqual(string firstPath, string secondPath)
    {
        var first = new FileInfo(firstPath);
        var second = new FileInfo(secondPath);
        if (first.Length != second.Length)
        {
            return false;
        }

        using var firstStream = File.OpenRead(firstPath);
        using var secondStream = File.OpenRead(secondPath);
        int firstByte;
        while ((firstByte = firstStream.ReadByte()) != -1)
        {
            if (firstByte != secondStream.ReadByte())
            {
                return false;
            }
        }

        return secondStream.ReadByte() == -1;
    }

    private static string CreateConflictPath(string targetFile)
    {
        var directory = Path.GetDirectoryName(targetFile)!;
        var fileName = Path.GetFileNameWithoutExtension(targetFile);
        var extension = Path.GetExtension(targetFile);

        for (var index = 1; ; index++)
        {
            var candidate = Path.Combine(directory, $"{fileName}.legacy-{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
