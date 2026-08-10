namespace SMMDownloader.Avalonia.Models;

public sealed class NotificationEntry
{
    public string Timestamp { get; init; } = "";
    public string Message { get; init; } = "";
    public string DisplayText => $"{Timestamp}  {Message}";
}
