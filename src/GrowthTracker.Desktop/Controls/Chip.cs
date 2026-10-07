using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using GrowthTracker.Desktop.Presentation;
using Material.Icons;
using Material.Icons.Avalonia;

namespace GrowthTracker.Desktop.Controls;

/// <summary>
/// Pill label tinted with a semantic color: Area chips, status badges, plan-type chips (Stitch design system).
/// Status always pairs color with an icon and text, never color alone (UI Design §2.2).
/// </summary>
public sealed class Chip : Border
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Chip, string?>(nameof(Text));

    public static readonly StyledProperty<string> ColorHexProperty =
        AvaloniaProperty.Register<Chip, string>(nameof(ColorHex), "#6B7280");

    public static readonly StyledProperty<MaterialIconKind?> IconProperty =
        AvaloniaProperty.Register<Chip, MaterialIconKind?>(nameof(Icon));

    /// <summary>Shows a small dot instead of an icon (Area chips).</summary>
    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<Chip, bool>(nameof(ShowDot));

    private readonly TextBlock _text = new() { FontSize = 11, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly MaterialIcon _icon = new() { Width = 12, Height = 12, IsVisible = false };
    private readonly Ellipse _dot = new() { Width = 6, Height = 6, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };

    public Chip()
    {
        CornerRadius = new CornerRadius(999);
        Padding = new Thickness(8, 3);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _dot, _icon, _text } };
        Apply();
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string ColorHex { get => GetValue(ColorHexProperty); set => SetValue(ColorHexProperty, value); }
    public MaterialIconKind? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool ShowDot { get => GetValue(ShowDotProperty); set => SetValue(ShowDotProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == ColorHexProperty
            || change.Property == IconProperty || change.Property == ShowDotProperty)
            Apply();
    }

    private void Apply()
    {
        var color = Display.Brush(ColorHex);
        Background = Display.Tint(ColorHex);
        _text.Text = Text;
        _text.Foreground = color;
        _icon.Foreground = color;
        _icon.IsVisible = Icon is not null;
        if (Icon is { } kind)
            _icon.Kind = kind;
        _dot.Fill = color;
        _dot.IsVisible = ShowDot;
    }
}
