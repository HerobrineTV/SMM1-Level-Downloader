using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SMMDownloader.Avalonia.Services;

public sealed class MiiImageLoader
{
    private const int BpgcMiiOffset = 0x18;
    private const int StoreDataLength = 96;
    private static readonly IImage PlaceholderImage = CreatePlaceholderImage();
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, IImage> _cache = new();

    public async Task<IImage> LoadAsync(string? miiData, CancellationToken cancellationToken)
    {
        var normalized = NormalizeMiiData(miiData);
        if (normalized == null)
        {
            return PlaceholderImage;
        }

        if (_cache.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        try
        {
            var url = "https://mii-unsecure.ariankordi.net/miis/image.png" +
                      $"?data={Uri.EscapeDataString(normalized)}" +
                      "&type=face&expression=normal&width=96&bgColor=00000000&verifyCRC16=false";
            var bytes = await _httpClient.GetByteArrayAsync(url, cancellationToken);
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            _cache[normalized] = bitmap;
            return bitmap;
        }
        catch
        {
            _cache[normalized] = PlaceholderImage;
            return PlaceholderImage;
        }
    }

    private static IImage CreatePlaceholderImage()
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            var borderPen = new Pen(new SolidColorBrush(Color.Parse("#8F4F17")), 2);
            var questionPen = new Pen(new SolidColorBrush(Color.Parse("#8F4F17")), 7)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 96, 96));
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#E9D58A")), borderPen, new Point(48, 48), 37, 37);
            context.DrawGeometry(null, questionPen, StreamGeometry.Parse("M 34 35 C 34 24 43 18 52 20 C 63 22 69 31 66 42 C 64 50 55 53 51 59 C 49 62 49 65 49 68"));
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#8F4F17")), null, new Point(49, 78), 4, 4);
        }

        return new DrawingImage(group);
    }

    private static string? NormalizeMiiData(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        try
        {
            var bytes = Convert.FromBase64String(ToStandardBase64(trimmed));
            if (IsBpgcContainer(bytes))
            {
                var storeData = new byte[StoreDataLength];
                Array.Copy(bytes, BpgcMiiOffset, storeData, 0, StoreDataLength);
                return Convert.ToBase64String(storeData);
            }

            return bytes.Length is >= 46 and <= 96 ? Convert.ToBase64String(bytes) : null;
        }
        catch (FormatException)
        {
            return IsHex(trimmed) ? trimmed : null;
        }
    }

    private static string ToStandardBase64(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        return padding == 0 ? normalized : normalized.PadRight(normalized.Length + 4 - padding, '=');
    }

    private static bool IsBpgcContainer(byte[] bytes)
    {
        return bytes.Length >= BpgcMiiOffset + StoreDataLength &&
               bytes[0] == 'B' &&
               bytes[1] == 'P' &&
               (bytes[2] == 'G' || bytes[2] == 'F') &&
               bytes[3] == 'C';
    }

    private static bool IsHex(string value)
    {
        return value.Length % 2 == 0 &&
               value.Length is >= 92 and <= 192 &&
               value.All(Uri.IsHexDigit);
    }
}
