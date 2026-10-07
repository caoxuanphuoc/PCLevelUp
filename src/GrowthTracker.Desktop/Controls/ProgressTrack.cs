using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GrowthTracker.Desktop.Controls;

/// <summary>
/// Horizontal progress bar with an "expected" tick mark, so users see at a glance whether they are
/// ahead of or behind plan (UI Design §4, Progress Bar).
/// </summary>
public sealed class ProgressTrack : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ProgressTrack, double>(nameof(Value));

    /// <summary>Expected progress 0..100; negative hides the marker.</summary>
    public static readonly StyledProperty<double> ExpectedProperty =
        AvaloniaProperty.Register<ProgressTrack, double>(nameof(Expected), -1);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(Fill), Brushes.SteelBlue);

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(Track), new SolidColorBrush(Color.Parse("#E2E8F0")));

    public static readonly StyledProperty<IBrush?> MarkerProperty =
        AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(Marker), new SolidColorBrush(Color.Parse("#0F172A")));

    static ProgressTrack()
    {
        AffectsRender<ProgressTrack>(ValueProperty, ExpectedProperty, FillProperty, TrackProperty, MarkerProperty);
        HeightProperty.OverrideDefaultValue<ProgressTrack>(8);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Expected { get => GetValue(ExpectedProperty); set => SetValue(ExpectedProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public IBrush? Marker { get => GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;

        double radius = size.Height / 2;
        context.DrawRectangle(Track, null, new Rect(size), radius, radius);

        double fill = Math.Clamp(Value, 0, 100) / 100 * size.Width;
        if (fill > 0)
            context.DrawRectangle(Fill, null, new Rect(0, 0, Math.Max(fill, size.Height), size.Height), radius, radius);

        if (Expected >= 0)
        {
            double x = Math.Clamp(Expected, 0, 100) / 100 * size.Width;
            double markerWidth = 2;
            context.DrawRectangle(Marker, null,
                new Rect(Math.Clamp(x - markerWidth / 2, 0, size.Width - markerWidth), -2, markerWidth, size.Height + 4));
        }
    }
}
