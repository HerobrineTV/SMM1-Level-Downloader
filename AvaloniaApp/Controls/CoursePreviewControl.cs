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

    public static readonly StyledProperty<CoursePreview?> CourseProperty =
        AvaloniaProperty.Register<CoursePreviewControl, CoursePreview?>(nameof(Course));

    public static readonly StyledProperty<bool> ShowHiddenBlocksProperty =
        AvaloniaProperty.Register<CoursePreviewControl, bool>(nameof(ShowHiddenBlocks));

    public static readonly StyledProperty<bool> DebugLevelViewerProperty =
        AvaloniaProperty.Register<CoursePreviewControl, bool>(nameof(DebugLevelViewer));

    private Point? _debugPointerPosition;
    private DebugTileSelection? _debugAnchor;
    private RenderTargetBitmap? _courseBitmap;
    private CoursePreview? _cachedCourse;
    private bool _cachedShowHiddenBlocks;
    private PixelSize _cachedPixelSize;
    private DebugHitCache? _debugHitCache;
    private CoursePreview? _debugHitCacheCourse;

    static CoursePreviewControl()
    {
        AffectsRender<CoursePreviewControl>(CourseProperty);
        AffectsMeasure<CoursePreviewControl>(CourseProperty);
        AffectsRender<CoursePreviewControl>(ShowHiddenBlocksProperty);
        AffectsRender<CoursePreviewControl>(DebugLevelViewerProperty);
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

        if (TryDrawCachedCourse(context, course, width, height))
        {
            DrawDebugOverlay(context, course, height);
            return;
        }

        DrawCourseContent(context, course, width, height);
        DrawDebugOverlay(context, course, height);
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
            _cachedPixelSize != pixelSize)
        {
            ClearCourseBitmap();
            var bitmap = new RenderTargetBitmap(pixelSize);
            using (var bitmapContext = bitmap.CreateDrawingContext())
            {
                DrawCourseContent(bitmapContext, course, width, height);
            }

            _courseBitmap = bitmap;
            _cachedCourse = course;
            _cachedShowHiddenBlocks = ShowHiddenBlocks;
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

        var visibleObjects = course.Objects
            .Where(obj => ShowHiddenBlocks || !IsHiddenQuestionBlock(obj))
            .OrderBy(item => item.Z)
            .ToList();
        var drawableObjects = visibleObjects
            .Where(obj => ShouldDrawObject(course, obj))
            .ToList();

        DrawTracks(context, drawableObjects.Where(obj => obj.Type == 59).ToList(), height);

        foreach (var obj in drawableObjects)
        {
            if (obj.Type == 59)
            {
                continue;
            }

            DrawCourseObject(context, course, obj, height);
        }
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

        if (change.Property == DebugLevelViewerProperty && DebugLevelViewer ||
            change.Property != DebugLevelViewerProperty && change.Property != CourseProperty)
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
            lines.Add($"pos x={obj.X}, y={obj.Y}, z={obj.Z}, width={obj.Width}, height={obj.Height}, size={obj.Size}");
            lines.Add($"flags=0x{obj.Flags:X8}, childFlags=0x{obj.ChildFlags:X8}, extended=0x{obj.ExtendedData:X8} ({obj.ExtendedData})");
            lines.Add($"linkId={obj.LinkId}, effect={obj.Effect}, transform={obj.Transform}, childTransform={obj.ChildTransform}, wing={obj.Wing}");
        }
    }

    private static bool TryGetTile(Point position, CoursePreview course, out int tileX, out int tileY)
    {
        tileX = (int)Math.Floor(position.X / Tile);
        tileY = course.HeightBlocks - 1 - (int)Math.Floor(position.Y / Tile);
        return tileX >= 0 && tileY >= 0 && tileX < course.WidthBlocks && tileY < course.HeightBlocks;
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
        var width = Math.Max(1, Math.Abs(obj.Width));
        var height = Math.Max(1, Math.Abs(obj.Height));
        return new TileRect(obj.X, obj.Y, width, height);
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

    private static bool ContainsTile(TileRect rect, int tileX, int tileY)
    {
        return tileX >= rect.X &&
               tileX < rect.X + rect.Width &&
               tileY >= rect.Y &&
               tileY < rect.Y + rect.Height;
    }

    private static void DrawEllipse(DrawingContext context, IBrush brush, Rect rect)
    {
        context.DrawEllipse(brush, null, rect.Center, rect.Width / 2, rect.Height / 2);
    }

    private static void DrawCourseObject(DrawingContext context, CoursePreview course, CourseObjectPreview obj, double courseHeight)
    {
        var cells = SpriteMap.GetCells(obj).ToList();
        if (cells.Count == 0)
        {
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
                    DrawSpriteImage(context, bitmap, source, dest, ShouldFlipHorizontally(obj));
                }
            }
            else
            {
                DrawSpriteImage(context, bitmap, source, dest, ShouldFlipHorizontally(obj));
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

        var railPen = new Pen(new SolidColorBrush(Color.Parse("#293241")), 4);
        var railHighlight = new Pen(new SolidColorBrush(Color.Parse("#D7E1EA")), 1.4);
        var nodes = tracks
            .Select(track => new TrackNode(track, GetTrackCenter(track, courseHeight)))
            .ToList();

        foreach (var track in tracks)
        {
            var start = GetTrackCenter(track, courseHeight);
            var width = Math.Max(1, Math.Abs(track.Width));
            var height = Math.Max(1, Math.Abs(track.Height));
            Point? end = null;
            if (width > 1)
            {
                end = new Point(start.X + (width - 1) * Tile, start.Y);
            }
            else if (height > 1)
            {
                end = new Point(start.X, start.Y - (height - 1) * Tile);
            }

            if (end is { } endPoint)
            {
                DrawTrackSegment(context, railPen, railHighlight, start, endPoint);
            }
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = i + 1; j < nodes.Count; j++)
            {
                var a = nodes[i];
                var b = nodes[j];
                var sameRow = Math.Abs(a.Object.Y - b.Object.Y) <= 0 && Math.Abs(a.Object.X - b.Object.X) <= 1;
                var sameColumn = Math.Abs(a.Object.X - b.Object.X) <= 0 && Math.Abs(a.Object.Y - b.Object.Y) <= 1;
                if (sameRow || sameColumn)
                {
                    DrawTrackSegment(context, railPen, railHighlight, a.Center, b.Center);
                }
            }
        }

        foreach (var node in nodes)
        {
            var rect = new Rect(node.Center.X - 5, node.Center.Y - 5, 10, 10);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#D7E1EA")), new Pen(new SolidColorBrush(Color.Parse("#293241")), 2), rect.Center, rect.Width / 2, rect.Height / 2);
        }
    }

    private static void DrawTrackSegment(DrawingContext context, Pen railPen, Pen railHighlight, Point start, Point end)
    {
        context.DrawLine(railPen, start, end);
        context.DrawLine(railHighlight, start, end);
    }

    private static Point GetTrackCenter(CourseObjectPreview track, double courseHeight)
    {
        return new Point((track.X + 0.5) * Tile, courseHeight - ((track.Y + 0.5) * Tile));
    }

    private readonly record struct TrackNode(CourseObjectPreview Object, Point Center);

    private readonly record struct TileRect(int X, int Y, int Width, int Height);

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
                    foreach (var cell in Extend3x4(0, 0, Math.Max(1, obj.Width), Math.Max(1, obj.Height), new Dictionary<int, SpriteSource>
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
                    foreach (var cell in Extend3x4(0, 0, Math.Max(1, obj.Width), Math.Max(1, obj.Height), new Dictionary<int, SpriteSource>
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
