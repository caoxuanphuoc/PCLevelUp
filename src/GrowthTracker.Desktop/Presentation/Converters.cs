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

    /// <summary>A getting-started step: ticked when done, an empty circle otherwise.</summary>
    public static readonly IValueConverter DoneIcon =
        new FuncValueConverter<bool, Material.Icons.MaterialIconKind>(done =>
            done ? Material.Icons.MaterialIconKind.CheckCircle : Material.Icons.MaterialIconKind.CircleOutline);

    /// <summary>"Xem" for a logged session, "Ghi nhận" for one still waiting.</summary>
    public static readonly IValueConverter OpenOrLog = new FuncValueConverter<bool, string>(logged => logged ? "Xem" : "Ghi nhận");

    /// <summary>An orange top edge on Area cards with an At Risk Goal (UI Design §5.2).</summary>
    public static readonly IValueConverter AtRiskTopBorder =
        new FuncValueConverter<bool, Avalonia.Thickness>(atRisk => atRisk ? new Avalonia.Thickness(0, 4, 0, 0) : new Avalonia.Thickness(0));

    /// <summary>Dims inactive (archived) rows.</summary>
    public static readonly IValueConverter ActiveOpacity =
        new FuncValueConverter<bool, double>(active => active ? 1.0 : 0.55);
}
