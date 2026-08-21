using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using SMMDownloader.Avalonia.Models;

namespace SMMDownloader.Avalonia.Controls;

public sealed class CoursePreviewControl : Control
{
    private const double Tile = 18;
    private const int MaxCachedPixels = 20_000_000;
    private const int GroundStateCount = 72;
    private const string AssetRoot = "avares://SMMDownloader.Avalonia/Assets/CourseViewer";
    private static readonly HashSet<int> ObjectBoundsFromDataOrigin = [27];
    private static readonly RenderOptions PixelArtRenderOptions = new()
    {
        BitmapInterpolationMode = BitmapInterpolationMode.None
    };

    public static readonly StyledProperty<CoursePreview?> CourseProperty =
        AvaloniaProperty.Register<CoursePreviewControl, CoursePreview?>(nameof(Course));

    public static readonly StyledProperty<bool> ShowHiddenBlocksProperty =
        AvaloniaProperty.Register<CoursePreviewControl, bool>(nameof(ShowHiddenBlocks));

    public static readonly StyledProperty<bool> DebugLevelViewerProperty =
        AvaloniaProperty.Register<CoursePreviewControl, bool>(nameof(DebugLevelViewer));

    public static readonly StyledProperty<bool> DebugTileRegionsProperty =
        AvaloniaProperty.Register<CoursePreviewControl, bool>(nameof(DebugTileRegions));

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<CoursePreviewControl, bool>(nameof(ShowGrid), true);

    private Point? _debugPointerPosition;
    private DebugTileSelection? _debugAnchor;
    private RenderTargetBitmap? _courseBitmap;
    private CoursePreview? _cachedCourse;
    private bool _cachedShowHiddenBlocks;
    private bool _cachedDebugTileRegions;
    private bool _cachedShowGrid;
    private PixelSize _cachedPixelSize;
    private DebugHitCache? _debugHitCache;
    private CoursePreview? _debugHitCacheCourse;

    static CoursePreviewControl()
    {
        AffectsRender<CoursePreviewControl>(CourseProperty);
        AffectsMeasure<CoursePreviewControl>(CourseProperty);
        AffectsRender<CoursePreviewControl>(ShowHiddenBlocksProperty);
        AffectsRender<CoursePreviewControl>(DebugLevelViewerProperty);
        AffectsRender<CoursePreviewControl>(DebugTileRegionsProperty);
        AffectsRender<CoursePreviewControl>(ShowGridProperty);
    }

    public CoursePreview? Course
    {
        get => GetValue(CourseProperty);
        set => SetValue(CourseProperty, value);
    }

    public bool ShowHiddenBlocks
    {
        get => GetValue(ShowHiddenBlocksProperty);
        set => SetValue(ShowHiddenBlocksProperty, value);
    }

    public bool DebugLevelViewer
    {
        get => GetValue(DebugLevelViewerProperty);
        set => SetValue(DebugLevelViewerProperty, value);
    }

    public bool DebugTileRegions
    {
        get => GetValue(DebugTileRegionsProperty);
        set => SetValue(DebugTileRegionsProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Max(520, Course?.WidthBlocks * Tile ?? 520);
        return new Size(width, 27 * Tile);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var course = Course;
        if (course == null)
        {
            DrawCenteredText(context, "No course preview loaded", Bounds.Size);
            return;
        }

        var width = Math.Max(Bounds.Width, course.WidthBlocks * Tile);
        var height = course.HeightBlocks * Tile;

        using (context.PushRenderOptions(PixelArtRenderOptions))
        {
            if (TryDrawCachedCourse(context, course, width, height))
            {
                DrawDebugOverlay(context, course, height);
                return;
            }

            DrawCourseContent(context, course, width, height);
            DrawDebugOverlay(context, course, height);
        }
    }

    private bool TryDrawCachedCourse(DrawingContext context, CoursePreview course, double width, double height)
    {
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(height));
        if ((long)pixelWidth * pixelHeight > MaxCachedPixels)
        {
            ClearCourseBitmap();
            return false;
        }

        var pixelSize = new PixelSize(pixelWidth, pixelHeight);
        if (_courseBitmap == null ||
            !ReferenceEquals(_cachedCourse, course) ||
            _cachedShowHiddenBlocks != ShowHiddenBlocks ||
            _cachedDebugTileRegions != DebugTileRegions ||
            _cachedShowGrid != ShowGrid ||
            _cachedPixelSize != pixelSize)
        {
            ClearCourseBitmap();
            var bitmap = new RenderTargetBitmap(pixelSize);
            using (var bitmapContext = bitmap.CreateDrawingContext())
            using (bitmapContext.PushRenderOptions(PixelArtRenderOptions))
            {
                DrawCourseContent(bitmapContext, course, width, height);
            }

            _courseBitmap = bitmap;
            _cachedCourse = course;
            _cachedShowHiddenBlocks = ShowHiddenBlocks;
            _cachedDebugTileRegions = DebugTileRegions;
            _cachedShowGrid = ShowGrid;
            _cachedPixelSize = pixelSize;
        }

        context.DrawImage(
            _courseBitmap,
            new Rect(0, 0, pixelSize.Width, pixelSize.Height),
            new Rect(0, 0, pixelSize.Width, pixelSize.Height));
        return true;
    }

    private void ClearCourseBitmap()
    {
        _courseBitmap?.Dispose();
        _courseBitmap = null;
        _cachedCourse = null;
        _cachedPixelSize = default;
        ClearDebugHitCache();
    }

    private void ClearDebugHitCache()
    {
        _debugHitCache = null;
        _debugHitCacheCourse = null;
    }

    private void DrawCourseContent(DrawingContext context, CoursePreview course, double width, double height)
    {
        DrawCourseBackground(context, width, height, course.ThemeName);
        if (ShowGrid)
        {
            using (context.PushOpacity(0.45))
            {
                var gridPen = new Pen(new SolidColorBrush(Color.Parse("#7CB7D4")), 1);
                for (var x = 0; x <= course.WidthBlocks; x++)
                {
                    context.DrawLine(gridPen, new Point(x * Tile, 0), new Point(x * Tile, height));
                }

                for (var y = 0; y <= course.HeightBlocks; y++)
                {
                    context.DrawLine(gridPen, new Point(0, y * Tile), new Point(width, y * Tile));
                }
            }
        }

        var visibleObjects = course.Objects
            .Where(obj => ShowHiddenBlocks || !IsHiddenQuestionBlock(obj))
            .OrderBy(item => item.Z)
            .ToList();
        var drawableObjects = visibleObjects
            .Where(obj => ShouldDrawObject(course, obj))
            .ToList();
        var pipes = drawableObjects
            .Where(obj => obj.Type == 9)
            .ToList();
        var pipeContentObjects = drawableObjects
            .Where(obj => IsPipeContentObject(obj, pipes))
            .ToList();

        DrawTracks(context, drawableObjects.Where(obj => obj.Type == 59).ToList(), height);

        foreach (var obj in drawableObjects)
        {
            if (obj.Type == 59 || pipeContentObjects.Contains(obj))
            {
                continue;
            }

            DrawCourseObject(context, course, obj, height, obj.Type == 9 ? PipeDrawLayer.Body : PipeDrawLayer.All);
        }

        foreach (var pipe in pipes)
        {
            DrawCourseObject(context, course, pipe, height, PipeDrawLayer.Outlet);
        }

        foreach (var pipeContent in pipeContentObjects)
        {
            if (TryFindPipeForContent(pipeContent, pipes, out var pipe))
            {
                DrawPipeChild(context, course, pipe, pipeContent, height);
            }
        }

        foreach (var pipe in pipes)
        {
            if (TryCreatePipeChild(pipe, out var pipeChild))
            {
                DrawPipeChild(context, course, pipe, pipeChild, height);
            }
        }

        DrawDebugTileRegions(context, course, height);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!DebugLevelViewer || Course == null)
        {
            _debugPointerPosition = null;
            return;
        }

        var position = e.GetPosition(this);
        if (_debugPointerPosition is { } previous &&
            TryGetTile(previous, Course, out var previousX, out var previousY) &&
            TryGetTile(position, Course, out var nextX, out var nextY) &&
            previousX == nextX &&
            previousY == nextY)
        {
            return;
        }

        _debugPointerPosition = position;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!DebugLevelViewer || Course == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = point.Position;
        if (!TryGetTile(position, Course, out var tileX, out var tileY))
        {
            return;
        }

        if (_debugAnchor is { } anchor && anchor.X == tileX && anchor.Y == tileY)
        {
            _debugAnchor = null;
        }
        else
        {
            _debugAnchor = new DebugTileSelection(tileX, tileY, position);
        }

        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _debugPointerPosition = null;
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ClearCourseBitmap();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DebugLevelViewerProperty && DebugLevelViewer)
        {
            return;
        }

        if (change.Property != DebugLevelViewerProperty &&
            change.Property != DebugTileRegionsProperty &&
            change.Property != ShowGridProperty &&
            change.Property != CourseProperty)
        {
            return;
        }

        _debugPointerPosition = null;
        _debugAnchor = null;
        ClearCourseBitmap();
        InvalidateVisual();
    }

    private static void DrawCourseBackground(DrawingContext context, double width, double height, string themeName)
    {
        var top = themeName == "castle" || themeName == "ghostHouse" ? "#2B335E" : "#69C8FF";
        var bottom = themeName == "underground" ? "#2E204C" : themeName == "castle" ? "#5A2834" : "#CFF4FF";
        context.FillRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse(top), 0),
                new GradientStop(Color.Parse(bottom), 1)
            }
        }, new Rect(0, 0, width, height));

        using (context.PushOpacity(0.18))
        {
            var cloud = new SolidColorBrush(Colors.White);
            for (double x = 22; x < width; x += 180)
            {
                DrawEllipse(context, cloud, new Rect(x, 32, 42, 18));
                DrawEllipse(context, cloud, new Rect(x + 20, 22, 48, 24));
                DrawEllipse(context, cloud, new Rect(x + 50, 34, 38, 16));
            }
        }
    }

    private void DrawDebugTileRegions(DrawingContext context, CoursePreview course, double courseHeight)
    {
        if (!DebugTileRegions || !course.Objects.Any())
        {
            return;
        }

        foreach (var obj in course.Objects.OrderBy(item => item.Z))
        {
            var bounds = GetRawBounds(obj);
            var rect = new Rect(
                bounds.X * Tile,
                courseHeight - ((bounds.Y + bounds.Height) * Tile),
                bounds.Width * Tile,
                bounds.Height * Tile);
            var color = GetDebugRegionColor(obj);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(48, color.R, color.G, color.B)), rect);
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(155, color.R, color.G, color.B)), 1), rect);
        }
    }

    private static Color GetDebugRegionColor(CourseObjectPreview obj)
    {
        var palette = new[]
        {
            Color.Parse("#F45B69"),
            Color.Parse("#2EC4B6"),
            Color.Parse("#FF9F1C"),
            Color.Parse("#7B61FF"),
            Color.Parse("#4D96FF"),
            Color.Parse("#8AC926")
        };
        return palette[Math.Abs(obj.Type) % palette.Length];
    }

    private void DrawDebugOverlay(DrawingContext context, CoursePreview course, double courseHeight)
    {
        if (!DebugLevelViewer)
        {
            return;
        }

        DebugTileSelection? selection = _debugAnchor;
        if (selection == null &&
            _debugPointerPosition is { } position &&
            TryGetTile(position, course, out var hoverX, out var hoverY))
        {
            selection = new DebugTileSelection(hoverX, hoverY, position);
        }

        if (selection is not { } active)
        {
            return;
        }

        var rect = new Rect(active.X * Tile, courseHeight - ((active.Y + 1) * Tile), Tile, Tile);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(70, 255, 233, 99)), rect);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#FFB000")), 2), rect);
        DrawDebugPanel(context, course, active);
    }

    private void DrawDebugPanel(DrawingContext context, CoursePreview course, DebugTileSelection selection)
    {
        var lines = BuildDebugLines(course, selection.X, selection.Y, selection.Position);
        if (lines.Count == 0)
        {
            return;
        }

        const double fontSize = 11;
        const double lineHeight = 15;
        const double padding = 8;
        var formattedLines = lines
            .Select(line => new FormattedText(
                line,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                fontSize,
                Brushes.White))
            .ToList();

        var maxTextWidth = formattedLines.Count == 0 ? 0 : formattedLines.Max(line => line.Width);
        var panelWidth = Math.Min(720, Math.Max(260, maxTextWidth + padding * 2));
        var panelHeight = Math.Max(44, Math.Min(Math.Max(44, Bounds.Height - 12), formattedLines.Count * lineHeight + padding * 2));
        var x = selection.Position.X + 14;
        var y = selection.Position.Y + 14;
        if (x + panelWidth > Bounds.Width)
        {
            x = Math.Max(6, selection.Position.X - panelWidth - 14);
        }

        if (y + panelHeight > Bounds.Height)
        {
            y = Math.Max(6, Bounds.Height - panelHeight - 6);
        }

        var panel = new Rect(x, y, panelWidth, panelHeight);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 31, 35, 43)), panel, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#FFB000")), _debugAnchor == null ? 1 : 2), panel, 4);

        using (context.PushClip(panel))
        {
            var maxLines = Math.Max(1, (int)Math.Floor((panelHeight - padding * 2) / lineHeight));
            for (var i = 0; i < Math.Min(maxLines, formattedLines.Count); i++)
            {
                context.DrawText(formattedLines[i], new Point(x + padding, y + padding + i * lineHeight));
            }
        }
    }

    private IReadOnlyList<string> BuildDebugLines(CoursePreview course, int tileX, int tileY, Point position)
    {
        var hits = GetDebugHitCache(course)
            .GetHits(tileX, tileY)
            .OrderBy(item => item.Object.Z)
            .ToList();
        var renderedHits = hits
            .Where(item => item.Hit.RenderedSprite)
            .ToList();
        var rawOnlyHits = hits
            .Where(item => item.Hit.RawBounds && !item.Hit.RenderedSprite)
            .ToList();

        var lines = new List<string>
        {
            $"Tile: x={tileX}, y={tileY}",
            $"Screen: x={position.X:0.##}, y={position.Y:0.##}",
            $"Course: {course.Name}",
            $"Mode={course.Mode}, Theme={course.ThemeName} ({course.Theme}), Scroll={course.ScrollName} ({course.Scroll}), WidthBlocks={course.WidthBlocks}",
            $"Rendered objects on tile: {renderedHits.Count}",
            $"Raw bounds only on tile: {rawOnlyHits.Count}"
        };

        AddDebugObjectLines(lines, renderedHits, "Rendered");
        AddDebugObjectLines(lines, rawOnlyHits, "Raw bounds only");

        return lines;
    }

    private DebugHitCache GetDebugHitCache(CoursePreview course)
    {
        if (_debugHitCache != null && ReferenceEquals(_debugHitCacheCourse, course))
        {
            return _debugHitCache;
        }

        _debugHitCache = DebugHitCache.Build(course);
        _debugHitCacheCourse = course;
        return _debugHitCache;
    }

    private static void AddDebugObjectLines(
        ICollection<string> lines,
        IEnumerable<DebugObjectHit> objects,
        string heading)
    {
        var list = objects.ToList();
        if (list.Count == 0)
        {
            return;
        }

        lines.Add("");
        lines.Add($"{heading}:");
        foreach (var item in list)
        {
            var obj = item.Object;
            lines.Add("");
            lines.Add($"#{item.Index} {obj.Name} [{(obj.IsBlock ? "Block" : "Object")}]");
            lines.Add($"match={FormatTileHit(item.Hit)}");
            lines.Add($"type={obj.Type}, subtype={obj.SubType}, childType={obj.ChildType}");
            lines.Add($"construct={FormatObjectConstruct(obj)}");
            lines.Add($"pos x={obj.X}, y={obj.Y}, z={obj.Z}, width={obj.Width}, height={obj.Height}, size={obj.Size}");
            lines.Add($"rawPos x={obj.RawX}, y={obj.RawY}, z={obj.RawZ}, rem x={FloorMod(obj.RawX, 160)}, y={FloorMod(obj.RawY, 160)}, z={FloorMod(obj.RawZ, 160)}");
            lines.Add($"flags=0x{obj.Flags:X8}, childFlags=0x{obj.ChildFlags:X8}, extended=0x{obj.ExtendedData:X8} ({obj.ExtendedData})");
            lines.Add($"linkId={obj.LinkId}, effect={obj.Effect}, transform={obj.Transform}, childTransform={obj.ChildTransform}, wing={obj.Wing}");
            if (IsDirectionalObject(obj))
            {
                lines.Add($"direction={GetDirectionalObjectDirection(obj)}, rotation={GetDirectionalObjectRotation(obj) * 180 / Math.PI:0.#}deg");
                lines.Add($"direction candidates={FormatDirectionCandidates(obj)}");
            }
        }
    }

    private static string FormatObjectConstruct(CourseObjectPreview obj)
    {
        if (obj.RawDataHex.Length < 64)
        {
            return obj.RawDataHex;
        }

        return string.Join(" ",
            $"00:x={obj.RawDataHex[..8]}",
            $"04:z={obj.RawDataHex.Substring(8, 8)}",
            $"08:y={obj.RawDataHex.Substring(16, 4)}",
            $"0A:w={obj.RawDataHex.Substring(20, 2)}",
            $"0B:h={obj.RawDataHex.Substring(22, 2)}",
            $"0C:flags={obj.RawDataHex.Substring(24, 8)}",
            $"10:childFlags={obj.RawDataHex.Substring(32, 8)}",
            $"14:ext={obj.RawDataHex.Substring(40, 8)}",
            $"18:type={obj.RawDataHex.Substring(48, 2)}",
            $"19:child={obj.RawDataHex.Substring(50, 2)}",
            $"1A:link={obj.RawDataHex.Substring(52, 4)}",
            $"1C:effect={obj.RawDataHex.Substring(56, 4)}",
            $"1E:tr={obj.RawDataHex.Substring(60, 2)}",
            $"1F:childTr={obj.RawDataHex.Substring(62, 2)}");
    }

    private static bool TryGetTile(Point position, CoursePreview course, out int tileX, out int tileY)
    {
        tileX = (int)Math.Floor(position.X / Tile);
        tileY = course.HeightBlocks - 1 - (int)Math.Floor(position.Y / Tile);
        return tileX >= 0 && tileY >= 0 && tileX < course.WidthBlocks && tileY < course.HeightBlocks;
    }

    private static int FloorMod(int value, int divisor)
    {
        var mod = value % divisor;
        return mod < 0 ? mod + divisor : mod;
    }

    private static TileHit GetTileHit(CoursePreview course, CourseObjectPreview obj, int tileX, int tileY)
    {
        var rawBounds = ContainsTile(GetRawBounds(obj), tileX, tileY);
        var renderedSprite = ShouldDrawObject(course, obj) &&
                             GetRenderedTileRects(obj).Any(rect => ContainsTile(rect, tileX, tileY));
        return new TileHit(rawBounds, renderedSprite);
    }

    private static bool ShouldDrawObject(CoursePreview course, CourseObjectPreview obj)
    {
        if (obj.Type == 49 && course.ThemeName != "castle")
        {
            return false;
        }

        if (obj.Type != 7)
        {
            return true;
        }

        var renderedRects = GetRenderedTileRects(obj).ToList();
        return renderedRects.Count == 0 ||
               renderedRects.Any(rect => !IsCoveredByBaseTerrain(course, rect));
    }

    private static bool IsCoveredByBaseTerrain(CoursePreview course, TileRect rect)
    {
        foreach (var terrain in course.Objects.Where(IsBaseTerrain))
        {
            foreach (var terrainRect in GetRenderedTileRects(terrain))
            {
                if (ContainsTile(terrainRect, rect.X, rect.Y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsBaseTerrain(CourseObjectPreview obj)
    {
        return obj.Type is 26 or 37;
    }

    private static string FormatTileHit(TileHit hit)
    {
        return hit switch
        {
            { RawBounds: true, RenderedSprite: true } => "raw bounds + rendered sprite",
            { RawBounds: true } => "raw bounds only",
            { RenderedSprite: true } => "rendered sprite only",
            _ => "none"
        };
    }

    private static TileRect GetRawBounds(CourseObjectPreview obj)
    {
        if (obj.Type == 9)
        {
            return GetPipeBounds(obj);
        }

        if (!obj.IsBlock &&
            !ObjectBoundsFromDataOrigin.Contains(obj.Type) &&
            TryGetRenderedTileBounds(obj, out var renderedBounds))
        {
            return renderedBounds;
        }

        var width = Math.Max(1, Math.Abs(obj.Width));
        var height = Math.Max(1, Math.Abs(obj.Height));
        if (!obj.IsBlock)
        {
            width = Math.Max(width, obj.Size);
            height = Math.Max(height, obj.Size == 2 ? 2 : 1);
            var x = obj.Size > 1 ? obj.X - (width / 2) : obj.X;
            return new TileRect(x, obj.Y, width, height);
        }

        return new TileRect(obj.X, obj.Y, width, height);
    }

    private static bool TryGetRenderedTileBounds(CourseObjectPreview obj, out TileRect bounds)
    {
        var rects = GetRenderedTileRects(obj).ToList();
        if (rects.Count == 0)
        {
            bounds = default;
            return false;
        }

        var minX = rects.Min(rect => rect.X);
        var minY = rects.Min(rect => rect.Y);
        var maxX = rects.Max(rect => rect.X + rect.Width);
        var maxY = rects.Max(rect => rect.Y + rect.Height);
        bounds = new TileRect(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    private static IEnumerable<TileRect> GetRenderedTileRects(CourseObjectPreview obj)
    {
        foreach (var cell in SpriteMap.GetCells(obj))
        {
            var size = Math.Max(1, obj.Size);
            var x = obj.Size == 1
                ? obj.X + (int)Math.Floor(cell.X)
                : obj.X + (int)Math.Floor(cell.X * size) - (2 - (int)Math.Ceiling(obj.Width / 2.0));
            var y = obj.Size == 1
                ? obj.Y + (int)Math.Floor(cell.Y)
                : obj.Y + (int)Math.Floor(cell.Y * size);
            yield return new TileRect(x, y, size, size);
        }
    }

    private static IEnumerable<SpriteCell> GetCellsForPipeLayer(CourseObjectPreview obj, PipeDrawLayer pipeLayer)
    {
        var cells = SpriteMap.GetCells(obj);
        if (obj.Type != 9 || pipeLayer == PipeDrawLayer.All)
        {
            return cells;
        }

        return cells.Where(cell => pipeLayer == PipeDrawLayer.Outlet
            ? IsPipeOutletCell(obj, cell)
            : !IsPipeOutletCell(obj, cell));
    }

    private static bool IsPipeOutletCell(CourseObjectPreview pipe, SpriteCell cell)
    {
        var direction = GetPipeDirection(pipe);
        var length = Math.Max(1, pipe.Height);
        return direction switch
        {
            0 => cell.X >= length - 1,
            1 => cell.X <= -(length - 1),
            2 => cell.Y >= length - 1,
            _ => cell.Y <= -(length - 1)
        };
    }

    private static bool IsPipeContentObject(CourseObjectPreview obj, IReadOnlyList<CourseObjectPreview> pipes)
    {
        return TryFindPipeForContent(obj, pipes, out _);
    }

    private static bool TryFindPipeForContent(
        CourseObjectPreview obj,
        IReadOnlyList<CourseObjectPreview> pipes,
        out CourseObjectPreview pipe)
    {
        pipe = null!;
        if (obj.Type is 9 or 59 || obj.IsBlock)
        {
            return false;
        }

        foreach (var candidate in pipes)
        {
            if (!IsPipeContentMatch(obj, candidate))
            {
                continue;
            }

            pipe = candidate;
            return true;
        }

        return false;
    }

    private static bool IsPipeContentMatch(CourseObjectPreview obj, CourseObjectPreview pipe)
    {
        return pipe.LinkId >= 0 && obj.LinkId == pipe.LinkId;
    }

    private static bool TryCreatePipeChild(CourseObjectPreview pipe, out CourseObjectPreview child)
    {
        child = null!;
        if (pipe.Type != 9 || pipe.ChildType < 0)
        {
            return false;
        }

        var childFlags = pipe.ChildFlags;
        var childType = pipe.ChildType;
        var isBlock = false;
        var size = childType != 3 ? (int)(((childFlags & 0x4000) >> 14) + 1) : 1;

        child = new CourseObjectPreview
        {
            X = pipe.X,
            Y = pipe.Y,
            Z = pipe.Z,
            Width = 1,
            Height = 1,
            Flags = childFlags,
            ChildFlags = 0,
            ExtendedData = pipe.ExtendedData,
            Type = childType,
            ChildType = -1,
            LinkId = pipe.LinkId,
            Effect = pipe.Effect,
            Transform = pipe.ChildTransform,
            ChildTransform = -1,
            Name = $"Pipe child type {childType}",
            IsBlock = isBlock,
            SubType = (int)((childFlags & 7 & 4) >> 2),
            Wing = (int)((childFlags & 7 & 2) >> 1),
            Size = Math.Max(1, size)
        };
        return true;
    }

    private static TileRect GetPipeBounds(CourseObjectPreview pipe)
    {
        var direction = GetPipeDirection(pipe);
        var length = Math.Max(1, pipe.Height);
        return direction switch
        {
            0 => new TileRect(pipe.X, pipe.Y - 1, length, 2),
            1 => new TileRect(pipe.X - (length - 1), pipe.Y, length, 2),
            2 => new TileRect(pipe.X, pipe.Y, 2, length),
            _ => new TileRect(pipe.X - 1, pipe.Y - (length - 1), 2, length)
        };
    }

    private static CourseRect GetPipeOutletPlacement(CourseObjectPreview pipe)
    {
        var direction = GetPipeDirection(pipe);
        var length = Math.Max(1, pipe.Height);
        return direction switch
        {
            0 => new CourseRect(pipe.X + length - 1, pipe.Y - 0.5, 1, 1),
            1 => new CourseRect(pipe.X - (length - 1), pipe.Y + 0.5, 1, 1),
            2 => new CourseRect(pipe.X + 0.5, pipe.Y + length - 1, 1, 1),
            _ => new CourseRect(pipe.X - 0.5, pipe.Y - (length - 1), 1, 1)
        };
    }

    private static int GetPipeDirection(CourseObjectPreview pipe)
    {
        return (int)((pipe.Flags & 0x60) / 0x20);
    }

    private static bool ContainsTile(TileRect rect, int tileX, int tileY)
    {
        return tileX >= rect.X &&
               tileX < rect.X + rect.Width &&
               tileY >= rect.Y &&
               tileY < rect.Y + rect.Height;
    }

    private static bool Intersects(TileRect a, TileRect b)
    {
        return a.X < b.X + b.Width &&
               a.X + a.Width > b.X &&
               a.Y < b.Y + b.Height &&
               a.Y + a.Height > b.Y;
    }

    private static void DrawEllipse(DrawingContext context, IBrush brush, Rect rect)
    {
        context.DrawEllipse(brush, null, rect.Center, rect.Width / 2, rect.Height / 2);
    }

    private static void DrawCourseObject(
        DrawingContext context,
        CoursePreview course,
        CourseObjectPreview obj,
        double courseHeight,
        PipeDrawLayer pipeLayer = PipeDrawLayer.All)
    {
        var cells = GetCellsForPipeLayer(obj, pipeLayer).ToList();
        if (cells.Count == 0)
        {
            if (pipeLayer != PipeDrawLayer.All)
            {
                return;
            }

            if (SpriteAssets.TryGetFormatSprite(obj, out var formatBitmap))
            {
                DrawFormatSprite(context, course, obj, courseHeight, formatBitmap);
                return;
            }

            DrawFallback(context, obj, courseHeight);
            return;
        }

        var bitmap = SpriteAssets.GetSheet(course, obj.IsBlock);
        var sourceSize = SpriteAssets.GetSourceTileSize(course.Mode, obj.IsBlock);
        if (bitmap == null)
        {
            if (SpriteAssets.TryGetFormatSprite(obj, out var formatBitmap))
            {
                DrawFormatSprite(context, course, obj, courseHeight, formatBitmap);
                return;
            }

            DrawFallback(context, obj, courseHeight);
            return;
        }

        var rotateDirectionalObject = IsDirectionalObject(obj);
        var rotation = rotateDirectionalObject ? GetDirectionalObjectRotation(obj) : 0;
        var center = GetObjectRenderCenter(obj, cells, courseHeight);
        IDisposable? transform = rotateDirectionalObject && Math.Abs(rotation) > double.Epsilon
            ? context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                                    Matrix.CreateRotation(rotation) *
                                    Matrix.CreateTranslation(center.X, center.Y))
            : null;
        using (transform)
        {

            foreach (var cell in cells)
            {
                var source = new Rect(cell.SourceX * sourceSize, cell.SourceY * sourceSize, sourceSize, sourceSize);
                var size = Math.Max(1, obj.Size);
                var destX = obj.Size == 1
                    ? (obj.X + cell.X) * Tile
                    : (obj.X + (cell.X * size) - (2 - Math.Ceiling(obj.Width / 2.0))) * Tile;
                var destY = obj.Size == 1
                    ? courseHeight - ((obj.Y + cell.Y + 1) * Tile)
                    : courseHeight - ((obj.Y + (cell.Y * size) + 2) * Tile);
                var dest = new Rect(destX, destY, Tile * size, Tile * size);
                if (dest.Right < 0 || dest.Left > course.WidthBlocks * Tile || dest.Bottom < 0 || dest.Top > courseHeight)
                {
                    continue;
                }

                if (cell.Opacity < 1)
                {
                    using (context.PushOpacity(cell.Opacity))
                    {
                        DrawSpriteImage(context, bitmap, source, dest, !rotateDirectionalObject && ShouldFlipHorizontally(obj));
                    }
                }
                else
                {
                    DrawSpriteImage(context, bitmap, source, dest, !rotateDirectionalObject && ShouldFlipHorizontally(obj));
                }
            }
        }
    }

    private static bool IsDirectionalObject(CourseObjectPreview obj)
    {
        return obj.Type is 66 or 67;
    }

    private static double GetDirectionalObjectRotation(CourseObjectPreview obj)
    {
        return GetDirectionalObjectDirection(obj) * (Math.PI / 4);
    }

    private static int GetDirectionalObjectDirection(CourseObjectPreview obj)
    {
        if (obj.Type == 66)
        {
            return GetAirSignBoardDirection(obj);
        }

        if (obj.Transform >= 0)
        {
            return obj.Transform % 8;
        }

        return (int)(obj.ExtendedData & 7);
    }

    private static int GetAirSignBoardDirection(CourseObjectPreview obj)
    {
        var state = GetAirSignBoardState(obj);
        return state switch
        {
            0xC => 2,
            0xE => 4,
            0xF => 6,
            _ => GetFallbackDirectionalObjectDirection(obj)
        };
    }

    private static int GetAirSignBoardState(CourseObjectPreview obj)
    {
        return (int)((obj.Flags >> 24) & 0xF);
    }

    private static int GetFallbackDirectionalObjectDirection(CourseObjectPreview obj)
    {
        foreach (var candidate in GetDirectionalObjectDirectionCandidates(obj))
        {
            if (candidate > 0)
            {
                return candidate % 8;
            }
        }

        return 0;
    }

    private static string FormatDirectionCandidates(CourseObjectPreview obj)
    {
        return string.Join(", ", GetNamedDirectionCandidates(obj)
            .Select(item => $"{item.Name}:{item.Value}"));
    }

    private static IEnumerable<(string Name, int Value)> GetNamedDirectionCandidates(CourseObjectPreview obj)
    {
        yield return ("transform", obj.Transform);
        yield return ("extended", (int)(obj.ExtendedData & 7));
        yield return ("flags28", (int)((obj.Flags >> 28) & 7));
        yield return ("airSignState", GetAirSignBoardState(obj));
        yield return ("flags23", (int)((obj.Flags >> 23) & 1));
        yield return ("flags24_4bit", (int)((obj.Flags >> 24) & 0xF));
        yield return ("flags24", (int)((obj.Flags >> 24) & 7));
        yield return ("flags20", (int)((obj.Flags >> 20) & 7));
        yield return ("flags16", (int)((obj.Flags >> 16) & 7));
        yield return ("flags12", (int)((obj.Flags >> 12) & 7));
        yield return ("flags8", (int)((obj.Flags >> 8) & 7));
        yield return ("flags4", (int)((obj.Flags >> 4) & 7));
        yield return ("childTransform", obj.ChildTransform);
    }

    private static double GetPipeChildRotation(CourseObjectPreview child)
    {
        return GetPipeChildDirection(child) * (Math.PI / 4);
    }

    private static int GetPipeChildDirection(CourseObjectPreview child)
    {
        if (child.Type == 66)
        {
            if (child.Transform >= 0)
            {
                return child.Transform % 8;
            }

            return GetAirSignBoardDirection(child);
        }

        if (child.ChildTransform > 0)
        {
            return child.ChildTransform % 8;
        }

        if (child.Transform >= 0)
        {
            return child.Transform % 8;
        }

        return GetFallbackDirectionalObjectDirection(child);
    }

    private static IEnumerable<int> GetDirectionalObjectDirectionCandidates(CourseObjectPreview obj)
    {
        return GetNamedDirectionCandidates(obj).Select(item => item.Value);
    }

    private static Point GetObjectRenderCenter(CourseObjectPreview obj, IReadOnlyList<SpriteCell> cells, double courseHeight)
    {
        var size = Math.Max(1, obj.Size);
        var minX = cells.Min(cell => obj.Size == 1
            ? obj.X + cell.X
            : obj.X + (cell.X * size) - (2 - Math.Ceiling(obj.Width / 2.0)));
        var maxX = cells.Max(cell => obj.Size == 1
            ? obj.X + cell.X + size
            : obj.X + (cell.X * size) - (2 - Math.Ceiling(obj.Width / 2.0)) + size);
        var minY = cells.Min(cell => obj.Size == 1
            ? obj.Y + cell.Y
            : obj.Y + (cell.Y * size));
        var maxY = cells.Max(cell => obj.Size == 1
            ? obj.Y + cell.Y + size
            : obj.Y + (cell.Y * size) + size);
        return new Point(((minX + maxX) / 2) * Tile, courseHeight - (((minY + maxY) / 2) * Tile));
    }

    private static void DrawPipeChild(
        DrawingContext context,
        CoursePreview course,
        CourseObjectPreview pipe,
        CourseObjectPreview child,
        double courseHeight)
    {
        var cells = SpriteMap.GetCells(child).ToList();
        if (cells.Count == 0)
        {
            return;
        }

        var bitmap = SpriteAssets.GetSheet(course, block: false);
        if (bitmap == null)
        {
            return;
        }

        var sourceSize = SpriteAssets.GetSourceTileSize(course.Mode, block: false);
        var size = Math.Max(1, child.Size);
        var minX = cells.Min(cell => cell.X * size);
        var maxX = cells.Max(cell => (cell.X * size) + size);
        var minY = cells.Min(cell => cell.Y * size);
        var maxY = cells.Max(cell => (cell.Y * size) + size);
        var sourceWidth = Math.Max(1, maxX - minX);
        var sourceHeight = Math.Max(1, maxY - minY);
        var target = GetPipeOutletPlacement(pipe);
        var targetCenterX = target.X + (target.Width / 2);
        var targetCenterY = target.Y + (target.Height / 2);
        var originX = targetCenterX - (sourceWidth / 2) - minX;
        var originY = targetCenterY - (sourceHeight / 2) - minY;
        var rotateDirectionalObject = IsDirectionalObject(child);
        var rotation = rotateDirectionalObject ? GetPipeChildRotation(child) : 0;
        var center = new Point(targetCenterX * Tile, courseHeight - (targetCenterY * Tile));
        IDisposable? transform = rotateDirectionalObject && Math.Abs(rotation) > double.Epsilon
            ? context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                                    Matrix.CreateRotation(rotation) *
                                    Matrix.CreateTranslation(center.X, center.Y))
            : null;

        using (transform)
        {
            foreach (var cell in cells)
            {
                var source = new Rect(cell.SourceX * sourceSize, cell.SourceY * sourceSize, sourceSize, sourceSize);
                var dest = new Rect(
                    (originX + (cell.X * size)) * Tile,
                    courseHeight - ((originY + (cell.Y * size) + size) * Tile),
                    Tile * size,
                    Tile * size);
                if (dest.Right < 0 || dest.Left > course.WidthBlocks * Tile || dest.Bottom < 0 || dest.Top > courseHeight)
                {
                    continue;
                }

                if (cell.Opacity < 1)
                {
                    using (context.PushOpacity(cell.Opacity))
                    {
                        DrawSpriteImage(context, bitmap, source, dest, !rotateDirectionalObject && ShouldFlipHorizontally(child));
                    }
                }
                else
                {
                    DrawSpriteImage(context, bitmap, source, dest, !rotateDirectionalObject && ShouldFlipHorizontally(child));
                }
            }
        }
    }

    private static void DrawSpriteImage(DrawingContext context, Bitmap bitmap, Rect source, Rect dest, bool flipHorizontally)
    {
        if (!flipHorizontally)
        {
            context.DrawImage(bitmap, source, dest);
            return;
        }

        using (context.PushTransform(Matrix.CreateTranslation(-(dest.Left + dest.Right), 0) * Matrix.CreateScale(-1, 1)))
        {
            var mirroredDest = new Rect(-dest.Right, dest.Top, dest.Width, dest.Height);
            context.DrawImage(bitmap, source, mirroredDest);
        }
    }

    private static bool ShouldFlipHorizontally(CourseObjectPreview obj)
    {
        return obj.Transform == 1 || obj.ChildTransform == 1;
    }

    private static bool IsHiddenQuestionBlock(CourseObjectPreview obj)
    {
        return obj.Type == 29;
    }

    private static void DrawTracks(DrawingContext context, IReadOnlyList<CourseObjectPreview> tracks, double courseHeight)
    {
        if (tracks.Count == 0)
        {
            return;
        }

        var segments = tracks
            .Select(CreateTrackSegment)
            .Where(segment => segment.HasLength)
            .ToList();

        foreach (var segment in segments)
        {
            DrawTrackSegment(context, segment, courseHeight);
        }

        var endpoints = segments
            .SelectMany(segment => new[] { segment.Start, segment.End })
            .Distinct()
            .ToList();
        foreach (var endpoint in endpoints)
        {
            DrawTrackNode(context, endpoint, courseHeight);
        }
    }

    private static TrackSegment CreateTrackSegment(CourseObjectPreview track)
    {
        var offsetX = GetSignedTrackOffset(track.Width) / 160.0;
        var offsetY = GetSignedTrackOffset(track.Height) / 160.0;
        return new TrackSegment(
            new Point(track.RawX / 160.0, track.RawY / 160.0),
            new Point(track.RawX / 160.0 + offsetX, track.RawY / 160.0 + offsetY));
    }

    private static int GetSignedTrackOffset(int value)
    {
        var length = Math.Abs(value);
        return length <= 1 ? 0 : Math.Sign(value) * (length - 1) * 32;
    }

    private static void DrawTrackSegment(DrawingContext context, TrackSegment segment, double courseHeight)
    {
        var start = ToScreenPoint(segment.Start, courseHeight);
        var end = ToScreenPoint(segment.End, courseHeight);
        var vector = end - start;
        var length = Math.Sqrt(Math.Pow(vector.X, 2) + Math.Pow(vector.Y, 2));
        if (length <= 0)
        {
            return;
        }

        if (TryDrawTexturedTrackSegment(context, start, length, vector))
        {
            return;
        }

        var direction = new Vector(vector.X / length, vector.Y / length);
        var normal = new Vector(-direction.Y, direction.X);
        var railOffset = normal * 3.2;
        var tieHalfLength = normal * 5.5;
        var tiePen = new Pen(new SolidColorBrush(Color.Parse("#E6EDF2")), 1.1);
        var tieShadowPen = new Pen(new SolidColorBrush(Color.Parse("#14171A")), 2.5);
        var railPen = new Pen(new SolidColorBrush(Color.Parse("#F2F7FA")), 2.4);
        var railShadowPen = new Pen(new SolidColorBrush(Color.Parse("#111417")), 4.8);

        for (var distance = 0.0; distance <= length + 0.1; distance += Tile / 2)
        {
            var center = start + (direction * distance);
            context.DrawLine(tieShadowPen, center - tieHalfLength, center + tieHalfLength);
            context.DrawLine(tiePen, center - tieHalfLength, center + tieHalfLength);
        }

        context.DrawLine(railShadowPen, start + railOffset, end + railOffset);
        context.DrawLine(railShadowPen, start - railOffset, end - railOffset);
        context.DrawLine(railPen, start + railOffset, end + railOffset);
        context.DrawLine(railPen, start - railOffset, end - railOffset);
    }

    private static bool TryDrawTexturedTrackSegment(DrawingContext context, Point start, double length, Vector vector)
    {
        var trackPreview = new CourseObjectPreview { Type = 59, Name = "Track" };
        if (!SpriteAssets.TryGetFormatSprite(trackPreview, out var bitmap))
        {
            return false;
        }

        const double trackHeight = Tile * 0.55;
        var angle = Math.Atan2(vector.Y, vector.X);
        using (context.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(start.X, start.Y)))
        {
            var source = new Rect(0, 0, bitmap.Size.Width, bitmap.Size.Height);
            var destination = new Rect(0, -trackHeight / 2, Math.Max(Tile * 0.4, length), trackHeight);
            context.DrawImage(bitmap, source, destination);
        }

        return true;
    }

    private static void DrawTrackNode(DrawingContext context, Point endpoint, double courseHeight)
    {
        var center = ToScreenPoint(endpoint, courseHeight);
        var outerBrush = new SolidColorBrush(Color.Parse("#111417"));
        var innerBrush = new SolidColorBrush(Color.Parse("#DCE6EC"));
        var capBrush = new SolidColorBrush(Color.Parse("#8B989F"));
        context.DrawEllipse(outerBrush, null, center, 5.2, 5.2);
        context.DrawEllipse(innerBrush, null, center, 3.6, 3.6);
        context.DrawEllipse(capBrush, null, center, 1.5, 1.5);
    }

    private static Point ToScreenPoint(Point coursePoint, double courseHeight)
    {
        return new Point(coursePoint.X * Tile, courseHeight - (coursePoint.Y * Tile));
    }

    private readonly record struct TrackSegment(Point Start, Point End)
    {
        public bool HasLength => Start != End;
    }

    private readonly record struct TileRect(int X, int Y, int Width, int Height);

    private readonly record struct CourseRect(double X, double Y, double Width, double Height);

    private enum PipeDrawLayer
    {
        All,
        Body,
        Outlet
    }

    private readonly record struct TileHit(bool RawBounds, bool RenderedSprite)
    {
        public bool Hit => RawBounds || RenderedSprite;
    }

    private readonly record struct DebugObjectHit(int Index, CourseObjectPreview Object, TileHit Hit);

    private readonly record struct DebugTileSelection(int X, int Y, Point Position);

    private sealed class DebugHitCache
    {
        private readonly Dictionary<(int X, int Y), List<DebugObjectHit>> _hits;

        private DebugHitCache(Dictionary<(int X, int Y), List<DebugObjectHit>> hits)
        {
            _hits = hits;
        }

        public static DebugHitCache Build(CoursePreview course)
        {
            var hits = new Dictionary<(int X, int Y), List<DebugObjectHit>>();
            for (var i = 0; i < course.Objects.Count; i++)
            {
                var obj = course.Objects[i];
                AddTileRectHits(hits, course, i, obj, GetRawBounds(obj), rawBounds: true);

                if (!ShouldDrawObject(course, obj))
                {
                    continue;
                }

                foreach (var rect in GetRenderedTileRects(obj))
                {
                    AddTileRectHits(hits, course, i, obj, rect, rawBounds: false);
                }

                if (TryCreatePipeChild(obj, out var pipeChild) && SpriteMap.GetCells(pipeChild).Any())
                {
                    AddTileRectHits(hits, course, course.Objects.Count + i, pipeChild, GetPipeBounds(obj), rawBounds: false);
                }
            }

            return new DebugHitCache(hits);
        }

        public IReadOnlyList<DebugObjectHit> GetHits(int tileX, int tileY)
        {
            return _hits.TryGetValue((tileX, tileY), out var hits) ? hits : [];
        }

        private static void AddTileRectHits(
            Dictionary<(int X, int Y), List<DebugObjectHit>> hits,
            CoursePreview course,
            int index,
            CourseObjectPreview obj,
            TileRect rect,
            bool rawBounds)
        {
            var startX = Math.Max(0, rect.X);
            var endX = Math.Min(course.WidthBlocks, rect.X + rect.Width);
            var startY = Math.Max(0, rect.Y);
            var endY = Math.Min(course.HeightBlocks, rect.Y + rect.Height);
            for (var x = startX; x < endX; x++)
            {
                for (var y = startY; y < endY; y++)
                {
                    AddTileHit(hits, (x, y), index, obj, rawBounds);
                }
            }
        }

        private static void AddTileHit(
            Dictionary<(int X, int Y), List<DebugObjectHit>> hits,
            (int X, int Y) tile,
            int index,
            CourseObjectPreview obj,
            bool rawBounds)
        {
            if (!hits.TryGetValue(tile, out var tileHits))
            {
                tileHits = [];
                hits[tile] = tileHits;
            }

            for (var i = 0; i < tileHits.Count; i++)
            {
                var hit = tileHits[i];
                if (hit.Index != index)
                {
                    continue;
                }

                tileHits[i] = hit with
                {
                    Hit = new TileHit(
                        hit.Hit.RawBounds || rawBounds,
                        hit.Hit.RenderedSprite || !rawBounds)
                };
                return;
            }

            tileHits.Add(new DebugObjectHit(index, obj, new TileHit(rawBounds, !rawBounds)));
        }
    }

    private static void DrawFormatSprite(DrawingContext context, CoursePreview course, CourseObjectPreview obj, double courseHeight, Bitmap bitmap)
    {
        var widthBlocks = Math.Max(1, obj.Width);
        var heightBlocks = Math.Max(1, obj.Height);
        if (!obj.IsBlock)
        {
            widthBlocks = Math.Max(widthBlocks, obj.Size);
            heightBlocks = Math.Max(heightBlocks, obj.Size == 2 ? 2 : 1);
        }

        var maxWidth = widthBlocks * Tile;
        var maxHeight = Math.Max(heightBlocks * Tile, Tile);
        var aspect = bitmap.Size.Width <= 0 || bitmap.Size.Height <= 0 ? 1 : bitmap.Size.Width / bitmap.Size.Height;
        var drawHeight = maxHeight;
        var drawWidth = drawHeight * aspect;
        if (drawWidth > maxWidth * 1.8)
        {
            drawWidth = maxWidth * 1.8;
            drawHeight = drawWidth / aspect;
        }

        var x = obj.X * Tile + ((maxWidth - drawWidth) / 2);
        var y = courseHeight - ((obj.Y + heightBlocks) * Tile) + (maxHeight - drawHeight);
        var dest = new Rect(x, y, drawWidth, drawHeight);
        if (dest.Right < 0 || dest.Left > course.WidthBlocks * Tile || dest.Bottom < 0 || dest.Top > courseHeight)
        {
            return;
        }

        context.DrawImage(bitmap, new Rect(0, 0, bitmap.Size.Width, bitmap.Size.Height), dest);
    }

    private static void DrawFallback(DrawingContext context, CourseObjectPreview obj, double courseHeight)
    {
        var objectWidth = Math.Max(1, obj.Width) * Tile;
        var objectHeight = Math.Max(1, obj.Height) * Tile;
        var x = obj.X * Tile;
        var y = courseHeight - ((obj.Y + Math.Max(1, obj.Height)) * Tile);
        if (double.IsNaN(y) || y < -objectHeight || y > courseHeight + objectHeight)
        {
            return;
        }

        var rect = new Rect(x, y, objectWidth, objectHeight);
        context.FillRectangle(new SolidColorBrush(Color.Parse(obj.IsBlock ? "#E8B354" : "#D95763")), rect);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#2C1600")), 1), rect);

        var label = obj.Type.ToString();
        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            9,
            Brushes.White);
        context.DrawText(text, new Point(x + 3, y + 2));
    }

    private static void DrawCenteredText(DrawingContext context, string value, Size size)
    {
        var text = new FormattedText(
            value,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            Brushes.Gray);
        context.DrawText(text, new Point((size.Width - text.Width) / 2, (size.Height - text.Height) / 2));
    }

    private static class SpriteAssets
    {
        private static readonly Dictionary<string, Bitmap?> Cache = [];
        private static readonly HashSet<int> SheetOnlyBlocks = [7, 9, 14, 16, 17, 26, 37, 49, 53, 59, 64];

        public static Bitmap? GetSheet(CoursePreview course, bool block)
        {
            var mode = NormalizeMode(course.Mode);
            var path = block
                ? $"{AssetRoot}/titleset/{mode}-{course.ThemeName}.png"
                : $"{AssetRoot}/monster/{mode}.png";

            if (Cache.TryGetValue(path, out var bitmap))
            {
                return bitmap;
            }

            try
            {
                bitmap = new Bitmap(AssetLoader.Open(new Uri(path)));
                Cache[path] = bitmap;
                return bitmap;
            }
            catch
            {
                Cache[path] = null;
                return null;
            }
        }

        public static int GetSourceTileSize(string mode, bool block)
        {
            var normalized = NormalizeMode(mode);
            if (normalized == "WU")
            {
                return block ? 64 : 128;
            }

            return 16;
        }

        private static string NormalizeMode(string mode)
        {
            return mode is "M1" or "M3" or "MW" or "WU" ? mode : "M1";
        }

        public static bool TryGetFormatSprite(CourseObjectPreview obj, out Bitmap bitmap)
        {
            bitmap = null!;
            if (obj.IsBlock && SheetOnlyBlocks.Contains(obj.Type))
            {
                return false;
            }

            foreach (var fileName in GetFormatCandidates(obj))
            {
                var path = $"{AssetRoot}/format/{fileName}";
                var loaded = LoadBitmap(path);
                if (loaded == null)
                {
                    continue;
                }

                bitmap = loaded;
                return true;
            }

            return false;
        }

        private static Bitmap? LoadBitmap(string path)
        {
            if (Cache.TryGetValue(path, out var bitmap))
            {
                return bitmap;
            }

            try
            {
                bitmap = new Bitmap(AssetLoader.Open(new Uri(path)));
                Cache[path] = bitmap;
                return bitmap;
            }
            catch
            {
                Cache[path] = null;
                return null;
            }
        }

        private static IEnumerable<string> GetFormatCandidates(CourseObjectPreview obj)
        {
            var subtype = obj.SubType;
            if (obj.Type == 3 && obj.Width > 1)
            {
                yield return "3-1.png";
            }

            if (obj.Type == 12 && subtype == 1)
            {
                yield return "12-1.png";
            }

            if (obj.Type == 13 && subtype == 1)
            {
                yield return "13-1.png";
            }

            if (obj.Type == 23 && ((obj.Flags >> 2) & 1) == 1)
            {
                yield return "23b.png";
            }

            if (obj.Type == 41 && ((obj.Flags & 256) >> 8) == 1)
            {
                yield return "41-x.png";
            }

            if (obj.Type == 55)
            {
                var doorType = (obj.Flags >> 18) & 3;
                yield return $"55-{doorType}.png";
            }

            yield return $"{obj.Type}-{subtype}.png";
            yield return $"{obj.Type}.png";
        }
    }

    private readonly record struct SpriteCell(double X, double Y, int SourceX, int SourceY, double Opacity = 1);

    private static class SpriteMap
    {
        private static readonly Dictionary<int, SpriteCell> BlockSingles = new()
        {
            [4] = new(0, 0, 1, 0),
            [5] = new(0, 0, 2, 0),
            [6] = new(0, 0, 6, 0),
            [21] = new(0, 0, 0, 4),
            [22] = new(0, 0, 6, 6),
            [29] = new(0, 0, 3, 0),
            [43] = new(0, 0, 2, 4),
            [59] = new(0, 0, 0, 8),
            [63] = new(0, 0, 8, 7)
        };

        public static IEnumerable<SpriteCell> GetCells(CourseObjectPreview obj)
        {
            return obj.IsBlock ? GetBlockCells(obj) : GetMonsterCells(obj);
        }

        private static IEnumerable<SpriteCell> GetBlockCells(CourseObjectPreview obj)
        {
            if (BlockSingles.TryGetValue(obj.Type, out var single))
            {
                yield return single;
                yield break;
            }

            switch (obj.Type)
            {
                case 7:
                    var offset = (int)Math.Clamp(obj.ExtendedData, 0, GroundStateCount - 1);
                    yield return new SpriteCell(0, 0, (8 + offset) % 16, 11 + ((8 + offset) / 16));
                    foreach (var cell in GroundDecorationCells(obj))
                    {
                        yield return cell;
                    }
                    break;
                case 8:
                    yield return ((obj.Flags & 4) >> 2) == 1 ? new SpriteCell(0, 0, 0, 16) : new SpriteCell(0, 0, 7, 0);
                    break;
                case 9:
                    foreach (var cell in PipeCells(obj))
                    {
                        yield return cell;
                    }
                    break;
                case 14:
                    foreach (var cell in MushroomTerrainCells(obj))
                    {
                        yield return cell;
                    }
                    break;
                case 16:
                    foreach (var cell in BoxTerrainCells(obj))
                    {
                        yield return cell;
                    }
                    break;
                case 17:
                    foreach (var cell in Extend3x4(0, 0, Math.Max(1, obj.Width), 2, new Dictionary<int, SpriteSource>
                             {
                                 [0] = new(0, 3), [1] = new(1, 3), [2] = new(2, 3),
                                 [9] = new(0, 2), [10] = new(1, 2), [11] = new(2, 2)
                             }))
                    {
                        yield return cell;
                    }
                    break;
                case 23:
                    yield return ((obj.Flags >> 2) & 1) == 1 ? new SpriteCell(0, 0, 6, 5) : new SpriteCell(0, 0, 4, 0);
                    break;
                case 26:
                    var (goalWidth, goalHeight) = GetBaseTerrainSize(obj);
                    foreach (var cell in Extend3x4(0, 0, goalWidth, goalHeight, new Dictionary<int, SpriteSource>
                             {
                                 [0] = new(11, 8), [1] = new(12, 8), [2] = new(12, 8),
                                 [3] = new(11, 8), [4] = new(12, 8), [5] = new(12, 8),
                                 [6] = new(11, 8), [7] = new(12, 8), [8] = new(12, 8),
                                 [9] = new(11, 7), [10] = new(12, 7), [11] = new(12, 7)
                             }))
                    {
                        yield return cell;
                    }
                    break;
                case 37:
                    var (startWidth, startHeight) = GetBaseTerrainSize(obj);
                    foreach (var cell in Extend3x4(0, 0, startWidth, startHeight, new Dictionary<int, SpriteSource>
                             {
                                 [0] = new(9, 8), [1] = new(9, 8), [2] = new(10, 8),
                                 [3] = new(9, 8), [4] = new(9, 8), [5] = new(10, 8),
                                 [6] = new(9, 8), [7] = new(9, 8), [8] = new(10, 8),
                                 [9] = new(9, 7), [10] = new(9, 7), [11] = new(10, 7)
                             }))
                    {
                        yield return cell;
                    }
                    break;
                case 49:
                    foreach (var cell in RowCells(Math.Max(1, obj.Width), 0, 15, 15))
                    {
                        yield return cell;
                    }
                    break;
                case 53:
                    foreach (var cell in Extend3x4(0, 0, Math.Max(1, obj.Width), 1, new Dictionary<int, SpriteSource>
                             {
                                 [0] = new(8, 0), [1] = new(9, 0), [2] = new(10, 0),
                                 [9] = new(8, 0), [10] = new(9, 0), [11] = new(10, 0)
                             }))
                    {
                        yield return cell;
                    }
                    break;
                case 64:
                    for (var y = 0; y < Math.Max(1, obj.Height); y++)
                    {
                        yield return new SpriteCell(0, y, y == 0 ? 13 : y == obj.Height - 1 ? 15 : 14, 7);
                    }
                    break;
            }
        }

        private static (int Width, int Height) GetBaseTerrainSize(CourseObjectPreview obj)
        {
            var width = Math.Max(1, Math.Abs(obj.Width));
            var height = Math.Max(1, Math.Abs(obj.Height));
            return (Math.Max(1, width - 3), height);
        }

        private static IEnumerable<SpriteCell> PipeCells(CourseObjectPreview obj)
        {
            var direction = (obj.Flags & 0x60) / 0x20;
            var height = Math.Max(1, obj.Height);
            if (direction == 0)
            {
                foreach (var cell in Extend3x4(0, -1, height, 2, new Dictionary<int, SpriteSource>
                         {
                             [0] = new(12, 1), [1] = new(12, 1), [2] = new(13, 1),
                             [9] = new(12, 0), [10] = new(12, 0), [11] = new(13, 0)
                         }))
                    yield return cell;
            }
            else if (direction == 1)
            {
                foreach (var cell in Extend3x4(-(height - 1), 0, height, 2, new Dictionary<int, SpriteSource>
                         {
                             [0] = new(11, 1), [1] = new(12, 1), [2] = new(12, 1),
                             [9] = new(11, 0), [10] = new(12, 0), [11] = new(12, 0)
                         }))
                    yield return cell;
            }
            else if (direction == 2)
            {
                foreach (var cell in Extend3x4(0, 0, 2, height, new Dictionary<int, SpriteSource>
                         {
                             [0] = new(14, 1), [2] = new(15, 1),
                             [3] = new(14, 1), [5] = new(15, 1),
                             [6] = new(14, 1), [8] = new(15, 1),
                             [9] = new(14, 0), [11] = new(15, 0)
                         }))
                    yield return cell;
            }
            else
            {
                foreach (var cell in Extend3x4(-1, -(height - 1), 2, height, new Dictionary<int, SpriteSource>
                         {
                             [0] = new(14, 2), [2] = new(15, 2),
                             [3] = new(14, 1), [5] = new(15, 1),
                             [6] = new(14, 1), [8] = new(15, 1),
                             [9] = new(14, 1), [11] = new(15, 1)
                         }))
                    yield return cell;
            }
        }

        private static IEnumerable<SpriteCell> GroundDecorationCells(CourseObjectPreview obj)
        {
            return obj.ChildType switch
            {
                1 => Cells(new SpriteCell(0, 2, 1, 1), new SpriteCell(0, 1, 2, 1)),
                2 => Cells(new SpriteCell(0, 3, 3, 1), new SpriteCell(0, 2, 4, 1), new SpriteCell(0, 1, 5, 1)),
                3 => Cells(new SpriteCell(0, 1, 13, 8), new SpriteCell(1, 1, 14, 8), new SpriteCell(2, 1, 15, 8)),
                0 => Cells(new SpriteCell(0, 1, 0, 1)),
                _ => []
            };
        }

        private static IEnumerable<SpriteCell> MushroomTerrainCells(CourseObjectPreview obj)
        {
            var width = Math.Max(1, obj.Width);
            var height = Math.Max(1, obj.Height);
            var color = (int)((obj.Flags >> 18) & 3);
            var headY = color == 1 ? 3 : color == 2 ? 4 : 2;
            var cells = Extend3x4(0, height - 1, width, 1, new Dictionary<int, SpriteSource>
            {
                [9] = new(3, headY), [10] = new(4, headY), [11] = new(5, headY)
            }).ToList();

            if (width % 2 == 0)
            {
                for (var y = 0; y < height - 1; y++)
                {
                    var x = (width / 2) - 1;
                    cells.Add(new SpriteCell(x, y, 6, y == 0 ? 2 : 1));
                    cells.Add(new SpriteCell(x + 1, y, 7, y == 0 ? 2 : 1));
                }
            }
            else
            {
                for (var y = 0; y < height - 1; y++)
                {
                    var x = (width - 1) / 2;
                    cells.Add(new SpriteCell(x, y, 6, y == 0 ? 4 : 3));
                }
            }

            return cells;
        }

        private static IEnumerable<SpriteCell> BoxTerrainCells(CourseObjectPreview obj)
        {
            var type = (int)((obj.Flags >> 18) & 3);
            var left = type switch
            {
                1 => 10,
                2 => 13,
                _ => 7
            };

            return Extend3x4(0, 0, Math.Max(1, obj.Width), Math.Max(1, obj.Height), new Dictionary<int, SpriteSource>
            {
                [0] = new(left, 6), [1] = new(left + 1, 6), [2] = new(left + 2, 6),
                [3] = new(left, 5), [4] = new(left + 1, 5), [5] = new(left + 2, 5),
                [6] = new(left, 4), [7] = new(left + 1, 4), [8] = new(left + 2, 4),
                [9] = new(left, 3), [10] = new(left + 1, 3), [11] = new(left + 2, 3)
            });
        }

        private static IEnumerable<SpriteCell> FillCells(CourseObjectPreview obj, int sourceX, int sourceY)
        {
            return FillCells(Math.Max(1, obj.Width), Math.Max(1, obj.Height), sourceX, sourceY);
        }

        private static IEnumerable<SpriteCell> FillCells(int width, int height, int sourceX, int sourceY)
        {
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    yield return new SpriteCell(x, y, sourceX + Math.Min(x, 2), sourceY + Math.Min(y, 2));
                }
            }
        }

        private static IEnumerable<SpriteCell> RowCells(int width, double y, int sourceX, int sourceY)
        {
            for (var x = 0; x < width; x++)
            {
                yield return new SpriteCell(x, y, sourceX, sourceY);
            }
        }

        private readonly record struct SpriteSource(int X, int Y);

        private static IEnumerable<SpriteCell> Extend3x4(int x, int y, int width, int height, Dictionary<int, SpriteSource> templates)
        {
            SpriteSource GetTemplate(int index) => templates.GetValueOrDefault(index, new SpriteSource(0, 0));

            for (var h = 0; h < height; h++)
            {
                for (var w = 0; w < width; w++)
                {
                    var templateIndex =
                        h == height - 1 && w == 0 ? 9 :
                        h == height - 1 && w == width - 1 ? 11 :
                        h == height - 1 ? 10 :
                        h == 0 && w == 0 ? 0 :
                        h == 0 && w == width - 1 ? 2 :
                        h == 0 ? 1 :
                        h % 2 == 1 && w == 0 ? 3 :
                        h % 2 == 1 && w == width - 1 ? 5 :
                        h % 2 == 1 ? 4 :
                        w == 0 ? 6 :
                        w == width - 1 ? 8 :
                        7;
                    var source = GetTemplate(templateIndex);
                    yield return new SpriteCell(w + x, h + y, source.X, source.Y);
                }
            }
        }

        private static IEnumerable<SpriteCell> GetMonsterCells(CourseObjectPreview obj)
        {
            switch (obj.Type)
            {
                case 0: return Cells(Extend(1, 2, 1, 1));
                case 1: return Cells(Extend(1, 2, 2, obj.SubType == 1 ? 3 : 1));
                case 2: return Cells(Extend(1, 2, 3, obj.SubType == 1 ? 3 : 1));
                case 3: return obj.Width > 1 ? Cells(Extend(2, 2, 5, 3)) : Cells(Extend(1, 2, 4, 1));
                case 10: return Cells(new SpriteCell(0, 0, 5, obj.SubType == 1 ? 1 : 0));
                case 11: return Cells(Extend(4, 1, 9, obj.SubType == 1 ? 1 : 0, 1));
                case 12: return obj.SubType == 1 ? Cells(Extend(4, 4, 3, 19, 1)) : Cells(Extend(2, 2, 11, 1));
                case 13: return CannonCells(obj);
                case 15: return Cells(new SpriteCell(0, 0, 13, obj.SubType == 1 ? 1 : 0));
                case 18: return Cells(new SpriteCell(0, 0, obj.SubType == 1 ? 4 : 14, obj.SubType == 1 ? 16 : 0));
                case 19: return Cells(new SpriteCell(0, 0, 14, 1));
                case 20: return Cells(new SpriteCell(0, 0, 15, 6));
                case 24: return Cells(new SpriteCell(0, 0, 13, 2), new SpriteCell(1, 0, 14, 2));
                case 25: return Cells(new SpriteCell(0, 0, 2, 4));
                case 27: return Cells(new SpriteCell(0, 0, 0, 0));
                case 28: return Cells(new SpriteCell(0, 0, 4, 4));
                case 30: return Cells(new SpriteCell(0, 0, 8, 2));
                case 31: return Cells(new SpriteCell(0, 0, 8, 3), new SpriteCell(1, 0, 9, 3), new SpriteCell(-1, 0, 9, 2));
                case 33: return Cells(new SpriteCell(0, 0, 15, 1));
                case 34: return Cells(new SpriteCell(0, 0, 15, 2));
                case 35: return Cells(new SpriteCell(0, 0, 15, 3));
                case 36: return Cells(new SpriteCell(1, 0, obj.SubType == 1 ? 11 : 10, 8), new SpriteCell(0, 0, obj.SubType == 1 ? 11 : 10, 8), new SpriteCell(-1, 0, obj.SubType == 1 ? 11 : 10, 8), new SpriteCell(-2, 0, obj.SubType == 1 ? 11 : 10, 8));
                case 38: return Cells(new SpriteCell(0, 0, 13, 5)).Concat(Extend(3, 2, 14, 4, 1, 1));
                case 39: return Cells(Extend(1, 2, 0, 6));
                case 40: return Cells(new SpriteCell(0, 0, obj.SubType == 1 ? 6 : 5, 4));
                case 41: return BooCells(obj);
                case 42: return obj.SubType == 1 ? Cells(Extend(2, 2, 5, 19)) : Cells(Extend(2, 2, 2, 6));
                case 44: return Cells(new SpriteCell(0, 0, 15, 5));
                case 45: return Cells(new SpriteCell(0, 0, 3, 5));
                case 46: return Cells(Extend(1, 2, 4, 6));
                case 47: return Cells(new SpriteCell(0, 0, obj.SubType == 1 ? 5 : 5, obj.SubType == 1 ? 6 : 5));
                case 48: return Cells(Extend(1, 2, obj.SubType == 1 ? 7 : 6, 6));
                case 50: return Cells(new SpriteCell(0, 0, 15, 4));
                case 51: return Cells(new SpriteCell(0, 0, 15, 0));
                case 52: return Cells(new SpriteCell(0.2, -0.2, 9, 4), new SpriteCell(0, 0, obj.SubType == 1 ? 9 : 8, 5));
                case 54: return Cells(new SpriteCell(0, 0, 9, 6)).Concat(Extend(1, 3, 10, 7, 0, 1));
                case 55: return Cells(Extend(1, 2, (((obj.Flags >> 18) & 3) == 1) ? 5 : (((obj.Flags >> 18) & 3) == 2) ? 6 : 11, (((obj.Flags >> 18) & 3) is 1 or 2) ? 17 : 6));
                case 56: return Cells(new SpriteCell(0, 0, 8, obj.SubType == 1 ? 7 : 6));
                case 57: return Cells(new SpriteCell(0, 0, obj.SubType == 1 ? 6 : 7, 7));
                case 58: return obj.SubType == 1 ? Cells(Extend(1, 1, 2, 7)) : Cells(Extend(1, 2, 3, 7));
                case 60: return Cells(new SpriteCell(0, 0, 1, 7));
                case 61: return obj.SubType == 1 ? Cells(new SpriteCell(0.3, 0.3, 12, 6), new SpriteCell(0, 0, 11, 7)) : Cells(new SpriteCell(0, 0, 11, 7));
                case 62: return obj.SubType == 1 ? Cells(Extend(2, 2, 4, 9, 1)) : Cells(Extend(3, 3, 2, 10));
                case 66: return obj.SubType == 1 ? Cells(Extend(2, 2, 9, 19)) : Cells(Extend(2, 2, 14, 7));
                case 67: return Cells(Extend(2, 2, 6, 9));
                case 68: return obj.SubType == 1 ? Cells(Extend(3, 3, 12, 19, 1)) : Cells(Extend(3, 3, 9, 10, 1));
                case 69: return Cells(new SpriteCell(0, 0, 14, 5));
                default: return [];
            }
        }

        private static IEnumerable<SpriteCell> CannonCells(CourseObjectPreview obj)
        {
            var height = Math.Max(1, obj.Height);
            for (var y = 0; y < height; y++)
            {
                var pos = y == height - 2 ? 1 : y == height - 1 ? 0 : 2;
                yield return obj.SubType == 1
                    ? new SpriteCell(0, y, 11, 2 + pos)
                    : new SpriteCell(0, y, 12, pos);
            }
        }

        private static IEnumerable<SpriteCell> BooCells(CourseObjectPreview obj)
        {
            if (obj.SubType == 1)
            {
                yield return new SpriteCell(-2, -2, 7, 4, 0.3);
                yield return new SpriteCell(-3, 0, 7, 4, 0.3);
                yield return new SpriteCell(-2, 2, 7, 4, 0.3);
                yield return new SpriteCell(0, 3, 7, 4, 0.3);
                yield return new SpriteCell(2, 2, 7, 4, 0.3);
                yield return new SpriteCell(3, 0, 7, 4, 0.3);
                yield return new SpriteCell(2, -2, 7, 4, 0.3);
            }

            yield return ((obj.Flags & 256) >> 8) == 1 ? new SpriteCell(0, 0, 9, 7) : new SpriteCell(0, 0, 7, 4);
        }

        private static IEnumerable<SpriteCell> Extend(int width, int height, int sourceX, int sourceY, double x = 0, double y = 0)
        {
            for (var w = 0; w < width; w++)
            {
                for (var h = 0; h < height; h++)
                {
                    yield return new SpriteCell(-w + x, h + y, sourceX - w, sourceY - h);
                }
            }
        }

        private static IEnumerable<SpriteCell> Cells(params SpriteCell[] cells)
        {
            return cells;
        }

        private static IEnumerable<SpriteCell> Cells(IEnumerable<SpriteCell> cells)
        {
            return cells;
        }
    }
}
