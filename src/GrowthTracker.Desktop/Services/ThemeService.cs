using Avalonia.Styling;

namespace GrowthTracker.Desktop.Services;

public interface IThemeService
{
    ThemeVariant RequestedTheme { get; }
    void SetTheme(ThemeVariant theme);
    /// <summary>Switches between explicit Light and Dark, starting from whatever is currently shown.</summary>
    void Toggle();
}

/// <summary>Follows the OS theme by default (ThemeVariant.Default). Persisting the choice comes with Settings.</summary>
public sealed class ThemeService : IThemeService
{
    private static Avalonia.Application App =>
        Avalonia.Application.Current ?? throw new InvalidOperationException("Avalonia application is not running.");

    public ThemeVariant RequestedTheme => App.RequestedThemeVariant ?? ThemeVariant.Default;

    public void SetTheme(ThemeVariant theme) => App.RequestedThemeVariant = theme;

    public void Toggle() =>
        SetTheme(App.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark);
}
