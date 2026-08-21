namespace SMMDownloader.Avalonia.Models;

public sealed class CoursePreview
{
    public string Name { get; init; } = "";
    public string Mode { get; init; } = "";
    public int Theme { get; init; }
    public string ThemeName { get; init; } = "";
    public int TimeLimit { get; init; }
    public int Scroll { get; init; }
    public string ScrollName { get; init; } = "";
    public int Width { get; init; }
    public int WidthBlocks { get; init; }
    public int HeightBlocks { get; init; } = 27;
    public int ObjectCount { get; init; }
    public IReadOnlyList<CourseObjectPreview> Objects { get; init; } = [];

    public string Summary =>
        $"{Name} ({Mode})\nTheme: {ThemeName} ({Theme}) | Time: {TimeLimit}s | Scroll: {ScrollName} ({Scroll}) over {WidthBlocks} blocks\nObjects: {ObjectCount}";
}

public sealed class CourseObjectPreview
{
    public string RawDataHex { get; init; } = "";
    public int RawX { get; init; }
    public int RawY { get; init; }
    public int RawZ { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public uint Flags { get; init; }
    public uint ChildFlags { get; init; }
    public uint ExtendedData { get; init; }
    public int Type { get; init; }
    public int ChildType { get; init; }
    public int LinkId { get; init; }
    public int Effect { get; init; }
    public int Transform { get; init; }
    public int ChildTransform { get; init; }
    public string Name { get; init; } = "";
    public bool IsBlock { get; init; }
    public int SubType { get; init; }
    public int Wing { get; init; }
    public int Size { get; init; } = 1;
}
