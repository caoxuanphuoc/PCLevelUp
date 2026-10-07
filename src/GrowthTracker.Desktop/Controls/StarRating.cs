using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;

namespace GrowthTracker.Desktop.Controls;

/// <summary>
/// 1–5 star Quality input (UI Design §4, Star Rating). Click a star, or focus it and use the arrow keys
/// (TDD §6.3). <see cref="IsReadOnly"/> shows stars without input.
/// </summary>
public sealed class StarRating : StackPanel
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<StarRating, int>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<StarRating, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<double> StarSizeProperty =
        AvaloniaProperty.Register<StarRating, double>(nameof(StarSize), 28);

    private static readonly IBrush Filled = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush Empty = new SolidColorBrush(Color.Parse("#CBD5E1"));
    private readonly MaterialIcon[] _stars = new MaterialIcon[5];

    public StarRating()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        Focusable = true;
        for (int i = 0; i < 5; i++)
        {
            int value = i + 1;
            var star = new MaterialIcon { Kind = MaterialIconKind.StarOutline, Cursor = new Cursor(StandardCursorType.Hand) };
            star.PointerPressed += (_, e) =>
            {
                if (IsReadOnly)
                    return;
                Value = value;
                Focus();
                e.Handled = true;
            };
            _stars[i] = star;
            Children.Add(star);
        }
        Apply();
    }

    public int Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public double StarSize { get => GetValue(StarSizeProperty); set => SetValue(StarSizeProperty, value); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsReadOnly)
            return;
        int next = e.Key switch
        {
            Key.Right or Key.Up => Math.Min(5, Value + 1),
            Key.Left or Key.Down => Math.Max(1, Value - 1),
            >= Key.D1 and <= Key.D5 => e.Key - Key.D0,
            _ => Value,
        };
        if (next != Value)
        {
            Value = next;
            e.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == StarSizeProperty || change.Property == IsReadOnlyProperty)
            Apply();
    }

    private void Apply()
    {
        for (int i = 0; i < 5; i++)
        {
            bool on = i < Value;
            _stars[i].Kind = on ? MaterialIconKind.Star : MaterialIconKind.StarOutline;
            _stars[i].Foreground = on ? Filled : Empty;
            _stars[i].Width = _stars[i].Height = StarSize;
            _stars[i].Cursor = IsReadOnly ? Cursor.Default : new Cursor(StandardCursorType.Hand);
        }
    }
}
