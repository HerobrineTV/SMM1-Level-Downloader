using System.Buffers.Binary;
using System.Text;
using SMMDownloader.Avalonia.Models;

namespace SMMDownloader.Avalonia.Services;

public sealed class SmmCourseParser
{
    private static readonly Dictionary<int, string> Themes = new()
    {
        [0] = "overworld",
        [1] = "underground",
        [2] = "castle",
        [3] = "airship",
        [4] = "water",
        [5] = "ghostHouse"
    };

    private static readonly Dictionary<int, string> Scrolls = new()
    {
        [0] = "none",
        [1] = "slow",
        [2] = "medium",
        [3] = "fast"
    };

    public static readonly Dictionary<int, string> BlockNames = new()
    {
        [4] = "Brick Block",
        [5] = "? Block",
        [6] = "Hard Block",
        [7] = "Ground",
        [8] = "Coin",
        [9] = "Pipe",
        [14] = "Mushroom Terrain",
        [16] = "Box Terrain",
        [17] = "Bridge",
        [21] = "Donut Block",
        [22] = "Cloud Block",
        [23] = "Note Block",
        [26] = "Goal Ground",
        [29] = "Clear Block",
        [37] = "Start Ground",
        [43] = "Spike Block",
        [49] = "Castle Bridge",
        [53] = "Conveyor",
        [59] = "Track",
        [63] = "Ice Block",
        [64] = "Vine Block"
    };

    public static readonly Dictionary<int, string> MonsterNames = new()
    {
        [0] = "Goomba",
        [1] = "Koopa Troopa",
        [2] = "Piranha Plant",
        [3] = "Hammer Bro",
        [10] = "Jump Platform",
        [11] = "Lift",
        [12] = "Thwomp",
        [13] = "Bullet Bill Cannon",
        [15] = "Bob-Omb",
        [18] = "P Switch",
        [19] = "POW Block",
        [20] = "Mushroom",
        [24] = "Fire Bar",
        [25] = "Spiny",
        [27] = "Goal Pole",
        [28] = "Buzzy Beetle",
        [30] = "Lakitu",
        [31] = "Lakitu Cloud",
        [32] = "Vine Head",
        [33] = "1-Up",
        [34] = "Fire Flower",
        [35] = "Super Star",
        [36] = "Lava Lift",
        [38] = "Start Sign",
        [39] = "Magikoopa",
        [40] = "Spike Top",
        [41] = "Boo",
        [42] = "Clown Car",
        [44] = "Costume Mushroom",
        [45] = "Shoe",
        [46] = "Dry Bones",
        [47] = "Cannon",
        [48] = "Blooper",
        [50] = "Weird Mushroom",
        [51] = "Mega Mushroom",
        [52] = "Wiggler",
        [54] = "Burner",
        [55] = "Door",
        [56] = "Cheep Cheep",
        [57] = "Muncher",
        [58] = "Rocky Wrench",
        [60] = "Podoboo",
        [61] = "Chain Chomp",
        [62] = "Bowser",
        [65] = "Player",
        [66] = "Airship Sign",
        [67] = "One-way Gate",
        [68] = "Grinder",
        [69] = "Player"
    };

    public CoursePreview Read(string courseDataPath)
    {
        var data = File.ReadAllBytes(courseDataPath);
        if (data.Length < 0xf0)
        {
            throw new InvalidDataException("Course file is too small.");
        }

        var theme = ReadUInt(data, 0x6d, 1);
        var scroll = ReadUInt(data, 0x72, 1);
        var areaWidth = ReadUInt(data, 0x74, 4);
        var objectCount = ReadUInt(data, 0xec, 4);
        if (objectCount > 2600)
        {
            throw new InvalidDataException($"Corrupted course file; object count is {objectCount}.");
        }

        var objects = new List<CourseObjectPreview>();
        for (var i = 0; i < objectCount; i++)
        {
            var pos = 0xf0 + 32 * i;
            if (pos + 32 > data.Length)
            {
                break;
            }

            objects.Add(ReadObject(data, pos));
        }

        return new CoursePreview
        {
            Name = ReadName(data, 0x28, 66),
            Mode = ReadAscii(data, 0x6a, 2),
            Theme = theme,
            ThemeName = Themes.GetValueOrDefault(theme, $"theme {theme}"),
            TimeLimit = ReadUInt(data, 0x70, 2),
            Scroll = scroll,
            ScrollName = Scrolls.GetValueOrDefault(scroll, $"scroll {scroll}"),
            Width = areaWidth,
            WidthBlocks = areaWidth / 16,
            ObjectCount = objectCount,
            Objects = objects
        };
    }

    private static CourseObjectPreview ReadObject(byte[] data, int pos)
    {
        var type = ReadSigned(data, pos + 0x18, 1);
        var rawX = ReadUInt(data, pos, 4);
        var rawZ = ReadUInt(data, pos + 0x04, 4);
        var rawY = ReadSigned(data, pos + 0x08, 2);
        var flags = ReadUInt32(data, pos + 0x0c, 4);
        var isBlock = BlockNames.ContainsKey(type);
        var subType = isBlock ? (int)((flags & 7) >> 2) : (int)((flags & 7 & 4) >> 2);
        var wing = (int)((flags & 7 & 2) >> 1);
        var size = !isBlock && type != 3 ? (int)(((flags & 0x4000) >> 14) + 1) : 1;
        var addY = type == 30 ? 1 : 0;

        return new CourseObjectPreview
        {
            RawDataHex = Convert.ToHexString(data.AsSpan(pos, 32)),
            RawX = rawX,
            RawY = rawY,
            RawZ = rawZ,
            X = rawX / 160,
            Z = rawZ / 160,
            Y = rawY / 160 + addY,
            Width = ReadSigned(data, pos + 0x0a, 1),
            Height = ReadSigned(data, pos + 0x0b, 1),
            Flags = flags,
            ChildFlags = ReadUInt32(data, pos + 0x10, 4),
            ExtendedData = ReadUInt32(data, pos + 0x14, 4),
            Type = type,
            ChildType = ReadSigned(data, pos + 0x19, 1),
            LinkId = ReadSigned(data, pos + 0x1a, 2),
            Effect = ReadSigned(data, pos + 0x1c, 2),
            Transform = ReadSigned(data, pos + 0x1e, 1),
            ChildTransform = ReadSigned(data, pos + 0x1f, 1),
            Name = BlockNames.GetValueOrDefault(type) ?? MonsterNames.GetValueOrDefault(type) ?? $"Type {type}",
            IsBlock = isBlock,
            SubType = subType,
            Wing = wing,
            Size = size
        };
    }

    private static int ReadUInt(byte[] data, int pos, int size)
    {
        return size switch
        {
            1 => data[pos],
            2 => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos, 2)),
            4 => unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos, 4))),
            _ => throw new ArgumentOutOfRangeException(nameof(size))
        };
    }

    private static uint ReadUInt32(byte[] data, int pos, int size)
    {
        return size == 4 ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos, 4)) : (uint)ReadUInt(data, pos, size);
    }

    private static int ReadSigned(byte[] data, int pos, int size)
    {
        var value = ReadUInt(data, pos, size);
        var bits = size * 8;
        var signBit = 1 << (bits - 1);
        var max = 1 << bits;
        return (value & signBit) != 0 ? value - max : value;
    }

    private static string ReadAscii(byte[] data, int pos, int size)
    {
        return Encoding.ASCII.GetString(data, pos, size).Trim('\0');
    }

    private static string ReadName(byte[] data, int pos, int size)
    {
        var raw = data.AsSpan(pos, size).ToArray();
        var name = Encoding.BigEndianUnicode.GetString(raw).Trim('\0');
        return string.IsNullOrWhiteSpace(name) ? "[unnamed]" : name;
    }
}
