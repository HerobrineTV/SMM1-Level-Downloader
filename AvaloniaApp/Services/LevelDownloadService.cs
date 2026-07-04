using System.Text.Json;
using SMMDownloader.Avalonia.Models;

namespace SMMDownloader.Avalonia.Services;

public sealed class LevelDownloadService(ProjectPaths paths, JsonStore store)
{
    private static readonly byte[] AshHeader = "ASH0"u8.ToArray();
    private static readonly string[] PartBaseNames = ["thumbnail0", "course_data", "course_data_sub", "thumbnail1"];
    private static readonly string[] OutputNames = ["thumbnail0.tnl", "course_data.cdt", "course_data_sub.cdt", "thumbnail1.tnl"];
    private readonly HttpClient _httpClient = new();

    public async Task DownloadAsync(
        LevelInfo level,
        string? packFolder,
        IProgress<string> status,
        CancellationToken cancellationToken)
    {
        var targetRoot = string.IsNullOrWhiteSpace(packFolder)
            ? paths.DownloadCacheDirectory
            : Path.Combine(paths.LevelPacksDirectory, packFolder);
        var targetDirectory = string.IsNullOrWhiteSpace(packFolder)
            ? Path.Combine(targetRoot, level.LevelId.ToString())
            : Path.Combine(targetRoot, GetCourseFolderName(level));

        if (Directory.Exists(targetDirectory))
        {
            throw new InvalidOperationException("Level folder already exists.");
        }

        Directory.CreateDirectory(targetRoot);
        status.Report("Fetching Wayback Machine URL...");
        var archiveUrl = await FetchArchiveUrlWithRetriesAsync(level.Url, cancellationToken);
        if (archiveUrl == null)
        {
            throw new InvalidOperationException("No archived version found on Wayback Machine.");
        }

        var packedFile = Path.Combine(targetRoot, GetPackedFileName(level));
        status.Report("Downloading packed course file...");
        await DownloadFileAsync(archiveUrl, packedFile, cancellationToken);

        await Task.Run(() =>
        {
            status.Report("Splitting course file...");
            SplitAshFile(packedFile, targetDirectory);

            status.Report("Preparing course files...");
            PrepareCourseParts(targetDirectory, status, cancellationToken);
        }, cancellationToken);

        File.Delete(packedFile);
        level.Pack = ResolvePackName(packFolder);
        level.Folder = targetDirectory;
        AddDownloadedLevel(level, packFolder);
        status.Report("Download complete.");
    }

    public void Delete(LevelInfo level)
    {
        var packs = store.LoadLevelPacks();
        var target = ResolveLevelFolder(level, packs);

        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }

        var downloaded = store.LoadDownloaded();
        downloaded.Remove(level.LevelId.ToString());
        if (!string.IsNullOrWhiteSpace(level.Pack))
        {
            downloaded.Remove($"{level.LevelId}_{level.Pack}");
        }

        store.SaveDownloaded(downloaded);
    }

    public async Task ResetOfficialCoursesAsync(IProgress<string> status, CancellationToken cancellationToken)
    {
        var source = Path.Combine(paths.OfficialCoursesDirectory, "OriginalFiles");
        var target = Path.Combine(paths.OfficialCoursesDirectory, "CourseFiles");
        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }

        Directory.CreateDirectory(target);
        if (!Directory.Exists(source))
        {
            Directory.CreateDirectory(source);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = Path.GetFileName(file);
            status.Report($"Resetting official course {id}...");
            await Task.Run(() =>
            {
                SplitAshFile(file, Path.Combine(target, id));
                PrepareCourseParts(Path.Combine(target, id), status, cancellationToken);
            }, cancellationToken);
        }
    }

    private async Task<string?> FetchArchiveUrlWithRetriesAsync(string originalUrl, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var result = await FetchArchiveUrlAsync(originalUrl, cancellationToken);
            if (result != null)
            {
                return result;
            }

            await Task.Delay(1000, cancellationToken);
        }

        return null;
    }

    private async Task<string?> FetchArchiveUrlAsync(string originalUrl, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(originalUrl);
        var apiUrl = $"https://web.archive.org/__wb/sparkline?output=json&url={encoded}&collection=web";
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("Accept-Language", "de,en-US;q=0.7,en;q=0.3");
        request.Headers.TryAddWithoutValidation("Referer", $"https://web.archive.org/web/20240000000000*/{encoded}");
        request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("first_ts", out var firstTs) ||
            firstTs.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var timestamp = firstTs.GetString();
        return string.IsNullOrWhiteSpace(timestamp)
            ? null
            : $"https://web.archive.org/web/{timestamp}if_/{originalUrl}";
    }

    private async Task DownloadFileAsync(string url, string outputPath, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language", "de,en-US;q=0.7,en;q=0.3");
        request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var network = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = File.Create(outputPath);
        await network.CopyToAsync(file, cancellationToken);
    }

    private void SplitAshFile(string inputFile, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        if (File.Exists(paths.SoundFile))
        {
            File.Copy(paths.SoundFile, Path.Combine(targetDirectory, "sound.bwv"), true);
        }

        var data = File.ReadAllBytes(inputFile);
        var starts = FindAshParts(data).ToList();
        for (var i = 0; i < starts.Count && i < PartBaseNames.Length; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1] : data.Length;
            File.WriteAllBytes(Path.Combine(targetDirectory, PartBaseNames[i]), data[start..end]);
        }
    }

    private void PrepareCourseParts(string targetDirectory, IProgress<string> status, CancellationToken cancellationToken)
    {
        for (var i = 0; i < PartBaseNames.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.Combine(targetDirectory, PartBaseNames[i]);
            if (!File.Exists(source))
            {
                continue;
            }

            var output = Path.Combine(targetDirectory, OutputNames[i]);
            status.Report($"Decompressing {OutputNames[i]}...");
            AshExtractor.ExtractFile(source, output);
            File.Delete(source);
        }
    }

    private void AddDownloadedLevel(LevelInfo level, string? packFolder)
    {
        var downloaded = store.LoadDownloaded();
        var packName = ResolvePackName(packFolder);
        var key = string.IsNullOrWhiteSpace(packName) ? level.LevelId.ToString() : $"{level.LevelId}_{packName}";
        downloaded[key] = level;
        store.SaveDownloaded(downloaded);
    }

    private string ResolveLevelFolder(LevelInfo level, Dictionary<string, string> packs)
    {
        if (!string.IsNullOrWhiteSpace(level.Folder) && Directory.Exists(level.Folder))
        {
            return level.Folder;
        }

        if (string.IsNullOrWhiteSpace(level.Pack))
        {
            return Path.Combine(paths.DownloadCacheDirectory, level.LevelId.ToString());
        }

        var packFolder = packs.GetValueOrDefault(level.Pack, level.Pack);
        var packRoot = Path.Combine(paths.LevelPacksDirectory, packFolder);
        var namedFolder = Path.Combine(packRoot, GetCourseFolderName(level));
        return Directory.Exists(namedFolder)
            ? namedFolder
            : Path.Combine(packRoot, level.LevelId.ToString());
    }

    private string? ResolvePackName(string? packFolder)
    {
        if (string.IsNullOrWhiteSpace(packFolder))
        {
            return null;
        }

        var packs = store.LoadLevelPacks();
        return packs.FirstOrDefault(item => item.Value == packFolder).Key ?? packFolder;
    }

    private static string GetPackedFileName(LevelInfo level)
    {
        if (Uri.TryCreate(level.Url, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath).TrimStart('0');
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return $"{level.LevelId}-00001";
    }

    private static string GetCourseFolderName(LevelInfo level)
    {
        var safeName = SanitizeFolderName(string.IsNullOrWhiteSpace(level.Name) ? level.LevelId.ToString() : level.Name);
        return $"{safeName}_{level.LevelId}";
    }

    private static string SanitizeFolderName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = value
            .Trim()
            .Select(character => invalid.Contains(character) ? '_' : character)
            .Select(character => char.IsControl(character) ? '_' : character);
        var sanitized = string.Concat(chars).Trim(' ', '.', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Unnamed_Course" : sanitized;
    }

    private static IEnumerable<int> FindAshParts(byte[] data)
    {
        for (var i = 0; i <= data.Length - AshHeader.Length; i++)
        {
            if (data.AsSpan(i, AshHeader.Length).SequenceEqual(AshHeader))
            {
                yield return i;
            }
        }
    }
}
