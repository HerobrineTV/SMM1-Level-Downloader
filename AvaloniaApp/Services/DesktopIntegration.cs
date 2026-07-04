using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SMMDownloader.Avalonia.Services;

public static class DesktopIntegration
{
    public static async Task<string?> PickFolderAsync(Window owner, string title)
    {
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = title
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public static void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", path);
            return;
        }

        Process.Start("xdg-open", path);
    }
}
