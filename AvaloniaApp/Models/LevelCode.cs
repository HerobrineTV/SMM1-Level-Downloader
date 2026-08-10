using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Buffers.Binary;

namespace SMMDownloader.Avalonia.Models;

public static class LevelCode
{
    private static readonly Regex CodePattern = new(
        @"^(?<checksum>[0-9A-Fa-f]{4})-?0000-?(?<high>[0-9A-Fa-f]{4})-?(?<low>[0-9A-Fa-f]{4})$",
        RegexOptions.Compiled);

    public static string Generate(long levelId)
    {
        var checksum = CalculateChecksum(levelId);
        var idString = levelId.ToString("X16");
        return $"{checksum}-0000-{idString[8..12]}-{idString[12..16]}";
    }

    public static long? TryParse(string code)
    {
        var trimmed = code.Trim();
        if (long.TryParse(trimmed, out var numericId))
        {
            return numericId;
        }

        var match = CodePattern.Match(trimmed);
        if (!match.Success)
        {
            return null;
        }

        var idString = match.Groups["high"].Value + match.Groups["low"].Value;
        if (!long.TryParse(idString, System.Globalization.NumberStyles.HexNumber, null, out var levelId))
        {
            return null;
        }

        var expectedChecksum = CalculateChecksum(levelId);
        return string.Equals(match.Groups["checksum"].Value, expectedChecksum, StringComparison.OrdinalIgnoreCase)
            ? levelId
            : null;
    }

    private static string CalculateChecksum(long levelId)
    {
        using var md5 = MD5.Create();
        using var hmac = new HMACMD5(md5.ComputeHash(Encoding.ASCII.GetBytes("9f2b4678")));

        var data = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(data, unchecked((ulong)levelId));
        var hash = hmac.ComputeHash(data);
        return $"{hash[3]:X2}{hash[2]:X2}";
    }
}
