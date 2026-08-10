using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace SMMDownloader.Avalonia.Controls;

public sealed class FloatingSpriteBackgroundControl : Control
{
    private const string AssetRoot = "avares://SMMDownloader.Avalonia/Assets/CourseViewer";
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new(1337);
    private readonly List<FloatingSprite> _sprites = [];
    private readonly Bitmap? _monsterSheet;

    public FloatingSpriteBackgroundControl()
    {
        IsHitTestVisible = false;
        _monsterSheet = LoadBitmap($"{AssetRoot}/monster/M1.png");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(42) };
        _timer.Tick += (_, _) =>
        {
            AdvanceSprites();
            InvalidateVisual();
        };
        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#64C8FF"), 0),
                new GradientStop(Color.Parse("#BFEFFF"), 0.52),
                new GradientStop(Color.Parse("#F2D06B"), 1)
            }
        }, bounds);

        DrawHills(context, bounds);
        DrawBlocks(context, bounds);

        if (_monsterSheet == null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        EnsureSprites(bounds);
        using (context.PushOpacity(0.2))
        {
            foreach (var sprite in _sprites)
            {
                var source = new Rect(sprite.SourceX * 16, sprite.SourceY * 16, 16, 16);
                var dest = new Rect(sprite.X, sprite.Y, sprite.Size, sprite.Size);
                context.DrawImage(_monsterSheet, source, dest);
            }
        }
    }

    private void EnsureSprites(Rect bounds)
    {
        if (_sprites.Count > 0)
        {
            return;
        }

        var choices = new[]
        {
            new Point(1, 1),
            new Point(2, 1),
            new Point(3, 1),
            new Point(7, 4),
            new Point(8, 6),
            new Point(11, 7),
            new Point(15, 3)
        };

        for (var i = 0; i < 18; i++)
        {
            var choice = choices[_random.Next(choices.Length)];
            _sprites.Add(new FloatingSprite(
                _random.NextDouble() * Math.Max(1, bounds.Width),
                _random.NextDouble() * Math.Max(1, bounds.Height * 0.72),
                22 + _random.NextDouble() * 24,
                0.25 + _random.NextDouble() * 0.55,
                0.2 + _random.NextDouble() * 0.5,
                (int)choice.X,
                (int)choice.Y));
        }
    }

    private void AdvanceSprites()
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        foreach (var sprite in _sprites)
        {
            sprite.X += sprite.Speed;
            sprite.Phase += 0.035;
            sprite.Y += Math.Sin(sprite.Phase) * 0.15;
            if (sprite.X > Bounds.Width + 48)
            {
                sprite.X = -64;
                sprite.Y = _random.NextDouble() * Math.Max(1, Bounds.Height * 0.7);
            }
        }
    }

    private static void DrawHills(DrawingContext context, Rect bounds)
    {
        using (context.PushOpacity(0.22))
        {
            var hill = new SolidColorBrush(Color.Parse("#4EB85C"));
            DrawEllipse(context, hill, new Rect(-90, bounds.Height - 170, 330, 210));
            DrawEllipse(context, hill, new Rect(bounds.Width - 230, bounds.Height - 145, 360, 200));
            DrawEllipse(context, new SolidColorBrush(Color.Parse("#80D477")), new Rect(bounds.Width * 0.36, bounds.Height - 120, 240, 150));
        }
    }

    private static void DrawEllipse(DrawingContext context, IBrush brush, Rect rect)
    {
        context.DrawEllipse(brush, null, rect.Center, rect.Width / 2, rect.Height / 2);
    }

    private static void DrawBlocks(DrawingContext context, Rect bounds)
    {
        var block = new SolidColorBrush(Color.Parse("#D98A35"));
        var stroke = new Pen(new SolidColorBrush(Color.Parse("#8C4A17")), 1.2);
        var y = bounds.Height - 34;
        for (double x = -8; x < bounds.Width + 32; x += 32)
        {
            var rect = new Rect(x, y, 32, 32);
            context.FillRectangle(block, rect);
            context.DrawRectangle(stroke, rect);
        }
    }

    private static Bitmap? LoadBitmap(string path)
    {
        try
        {
            return new Bitmap(AssetLoader.Open(new Uri(path)));
        }
        catch
        {
            return null;
        }
    }

    private sealed class FloatingSprite(double x, double y, double size, double speed, double phase, int sourceX, int sourceY)
    {
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double Size { get; } = size;
        public double Speed { get; } = speed;
        public double Phase { get; set; } = phase;
        public int SourceX { get; } = sourceX;
        public int SourceY { get; } = sourceY;
    }
}
