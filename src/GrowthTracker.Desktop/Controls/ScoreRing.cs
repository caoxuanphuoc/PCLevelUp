using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GrowthTracker.Desktop.Controls;

/// <summary>
/// Score 0–100 drawn as a ring, colored by score tier (UI Design §4, Score Ring). Put the number on top of it
/// with a TextBlock in the same Grid cell.
/// </summary>
public sealed class ScoreRing : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ScoreRing, IBrush?>(nameof(Stroke), Brushes.SteelBlue);

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<ScoreRing, IBrush?>(nameof(Track), new SolidColorBrush(Color.Parse("#E2E8F0")));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(Thickness), 10);

    static ScoreRing() => AffectsRender<ScoreRing>(ValueProperty, StrokeProperty, TrackProperty, ThicknessProperty);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    public override void Render(DrawingContext context)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= Thickness * 2)
            return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double radius = (size - Thickness) / 2;
        context.DrawEllipse(null, new Pen(Track, Thickness), center, radius, radius);

        double fraction = Math.Clamp(Value, 0, 100) / 100;
        if (fraction <= 0)
            return;
        var pen = new Pen(Stroke, Thickness, lineCap: PenLineCap.Round);
        if (fraction >= 0.999)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        // Clockwise from 12 o'clock.
        double angle = fraction * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false);
            ctx.ArcTo(end, new Size(radius, radius), 0, isLargeArc: fraction > 0.5, SweepDirection.Clockwise);
            ctx.EndFigure(isClosed: false);
        }
        context.DrawGeometry(null, pen, geometry);
    }
}
