namespace SMMDownloader.Avalonia.Models;

public static class ReleaseChannel
{
    public const string Stable = "stable";
    public const string Prerelease = "prerelease";

    public static string Normalize(string? value)
    {
        return string.Equals(value, Prerelease, StringComparison.OrdinalIgnoreCase) ? Prerelease : Stable;
    }

    public static bool IsPrerelease(string? value)
    {
        return string.Equals(Normalize(value), Prerelease, StringComparison.OrdinalIgnoreCase);
    }
}
