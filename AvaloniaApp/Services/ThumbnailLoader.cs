using System.Buffers.Binary;
using Avalonia.Media.Imaging;

namespace SMMDownloader.Avalonia.Services;

public sealed class ThumbnailLoader
{
    private const int JpegOffset = 8;
    private readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Bitmap? LoadCourseThumbnail(string courseFolder)
    {
        if (string.IsNullOrWhiteSpace(courseFolder))
        {
            return null;
        }

        var primary = Path.Combine(courseFolder, "thumbnail0.tnl");
        var secondary = Path.Combine(courseFolder, "thumbnail1.tnl");
        return LoadTnl(primary) ?? LoadTnl(secondary);
    }

    public void InvalidateCourseThumbnail(string courseFolder)
    {
        if (string.IsNullOrWhiteSpace(courseFolder))
        {
            return;
        }

        _cache.Remove(Path.Combine(courseFolder, "thumbnail0.tnl"));
        _cache.Remove(Path.Combine(courseFolder, "thumbnail1.tnl"));
    }

    private Bitmap? LoadTnl(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Bitmap? bitmap = null;
        try
        {
            if (File.Exists(path))
            {
                var data = File.ReadAllBytes(path);
                if (TryGetJpegSlice(data, out var offset, out var length))
                {
                    using var stream = new MemoryStream(data, offset, length, writable: false);
                    bitmap = new Bitmap(stream);
                }
            }
        }
        catch
        {
            bitmap = null;
        }

        if (bitmap != null)
        {
            _cache[path] = bitmap;
        }

        return bitmap;
    }

    private static bool TryGetJpegSlice(byte[] data, out int offset, out int length)
    {
        offset = 0;
        length = 0;

        if (data.Length <= JpegOffset + 4 ||
            data[JpegOffset] != 0xFF ||
            data[JpegOffset + 1] != 0xD8)
        {
            return false;
        }

        var declaredLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4)));
        offset = JpegOffset;
        length = declaredLength > 0 && offset + declaredLength <= data.Length
            ? declaredLength
            : data.Length - offset;
        return length > 4;
    }
}
