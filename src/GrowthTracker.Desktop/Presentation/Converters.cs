using Avalonia.Data.Converters;

namespace GrowthTracker.Desktop.Presentation;

/// <summary>Value converters for XAML: <c>{x:Static p:Converters.HexToBrush}</c>.</summary>
public static class Converters
{
    public static readonly IValueConverter HexToBrush =
        new FuncValueConverter<string?, Avalonia.Media.IBrush?>(hex => hex is null ? null : Display.Brush(hex));

    public static readonly IValueConverter HexToTint =
        new FuncValueConverter<string?, Avalonia.Media.IBrush?>(hex => hex is null ? null : Display.Tint(hex));

    public static readonly IValueConverter BoolToBold =
        new FuncValueConverter<bool, Avalonia.Media.FontWeight>(b => b ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal);

    /// <summary>Outlines today's circle in the week strip.</summary>
    public static readonly IValueConverter TodayRing =
        new FuncValueConverter<bool, Avalonia.Thickness>(b => b ? new Avalonia.Thickness(2) : new Avalonia.Thickness(0));

    /// <summary>Dims inactive (archived) rows.</summary>
    public static readonly IValueConverter ActiveOpacity =
        new FuncValueConverter<bool, double>(active => active ? 1.0 : 0.55);
}
