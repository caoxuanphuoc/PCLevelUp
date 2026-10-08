using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GrowthTracker.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop;

public partial class App : Avalonia.Application
{
    /// <summary>Root service provider, set by Program (or by tests) before Avalonia starts.</summary>
    public static IServiceProvider? Services { get; set; }

    /// <summary>Started with --minimized (e.g. at OS start-up): stay in the tray until opened.</summary>
    public static bool StartMinimized { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Services is not null)
        {
            GlobalExceptionHandlers.RegisterUiThread(Services);
            Services.GetRequiredService<AppShell>().Start(desktop, StartMinimized);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
