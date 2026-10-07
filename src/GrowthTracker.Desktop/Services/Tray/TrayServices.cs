using Avalonia.Controls;
using Avalonia.Platform;
using GrowthTracker.Application.Abstractions;

namespace GrowthTracker.Desktop.Services.Tray;

/// <summary>
/// The system tray icon (TDD §6.2): open the app, jump to Today, or exit. Lets the app keep running in the
/// background so reminders still fire after the window is closed.
/// </summary>
public interface ITrayService : IDisposable
{
    event EventHandler? OpenRequested;
    event EventHandler? OpenTodayRequested;
    event EventHandler? ExitRequested;

    void Show();
}

/// <summary>Cross-platform tray icon (used on Linux/macOS) through Avalonia's TrayIcon.</summary>
public sealed class AvaloniaTrayService : ITrayService
{
    private TrayIcon? _icon;

    public event EventHandler? OpenRequested;
    public event EventHandler? OpenTodayRequested;
    public event EventHandler? ExitRequested;

    public void Show()
    {
        if (_icon is not null)
            return;
        var open = new NativeMenuItem("Mở Growth Tracker");
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        var today = new NativeMenuItem("Hôm nay");
        today.Click += (_, _) => OpenTodayRequested?.Invoke(this, EventArgs.Empty);
        var exit = new NativeMenuItem("Thoát");
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _icon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://GrowthTracker.Desktop/Assets/app.ico"))),
            ToolTipText = "Growth Tracker",
            Menu = new NativeMenu { Items = { open, today, new NativeMenuItemSeparator(), exit } },
        };
        _icon.Clicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _icon.IsVisible = true;
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}

/// <summary>Used where there is no tray at all (headless tests): never shows anything.</summary>
public sealed class NoTrayService : ITrayService
{
    public event EventHandler? OpenRequested { add { } remove { } }
    public event EventHandler? OpenTodayRequested { add { } remove { } }
    public event EventHandler? ExitRequested { add { } remove { } }

    public void Show() { }

    public void Dispose() { }
}

/// <summary>Windows INotifier: reminders appear as notifications from the app's own tray icon.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class TrayNotifier(WindowsTrayIcon tray) : INotifier
{
    public Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => tray.ShowBalloon(title, message));
        return Task.CompletedTask;
    }
}
