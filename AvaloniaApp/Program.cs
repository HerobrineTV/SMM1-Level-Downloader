using Avalonia;
using System.Runtime.InteropServices;

namespace SMMDownloader.Avalonia;

internal static class Program
{
    private const string AppUserModelId = "TastelessStudios.SMM1LevelDownloader";

    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appId);
}
