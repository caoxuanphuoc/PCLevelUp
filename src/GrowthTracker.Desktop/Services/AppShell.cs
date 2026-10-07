using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Jobs;
using GrowthTracker.Application.Settings;
using GrowthTracker.Desktop.Services.Tray;
using GrowthTracker.Desktop.ViewModels;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Desktop.Views;
using Serilog;

namespace GrowthTracker.Desktop.Services;

/// <summary>Current user preferences, loaded at start-up and updated by the Settings page.</summary>
public sealed class AppPreferences
{
    public AppSettingsDto Current { get; private set; } = new(true, false, true, 10, null, null);

    public async Task LoadAsync(IUseCaseDispatcher useCases) => Current = await useCases.QueryAsync(new GetAppSettingsQuery());

    public void Update(AppSettingsDto settings) => Current = settings;
}

/// <summary>
/// Window lifecycle around the tray (TDD §6.2, §7.2): closing hides to the tray when enabled so reminders keep
/// running, the tray and a second launch bring the window back, and only "Thoát" really quits.
/// </summary>
public sealed class AppShell(MainWindowViewModel mainViewModel, ITrayService tray, AppPreferences preferences,
                             INavigationService navigation, INotifier notifier, JobScheduler scheduler)
{
    private IClassicDesktopStyleApplicationLifetime? _lifetime;
    private MainWindow? _window;
    private bool _exiting;
    private bool _toldAboutTray;

    public MainWindow? Window => _window;

    public void Start(IClassicDesktopStyleApplicationLifetime lifetime, bool startMinimized)
    {
        _lifetime = lifetime;
        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _window = new MainWindow { DataContext = mainViewModel };
        _window.Closing += OnClosing;

        tray.OpenRequested += (_, _) => ShowWindow();
        tray.OpenTodayRequested += (_, _) => ShowWindow(goToToday: true);
        tray.ExitRequested += (_, _) => Exit();
        try
        {
            tray.Show();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Tray icon unavailable");
        }

        // Background jobs changed data (rollover, risk scan): refresh the visible page.
        scheduler.DataChanged += (_, _) => Dispatcher.UIThread.Post(() => _ = navigation.RefreshAsync());

        if (!startMinimized)
            ShowWindow();
    }

    public void ShowWindow(bool goToToday = false)
    {
        if (_window is null)
            return;
        if (!_window.IsVisible)
            _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
        if (goToToday)
            _ = navigation.NavigateAsync<TodayViewModel>();
    }

    public void Exit()
    {
        _exiting = true;
        tray.Dispose();
        _lifetime?.Shutdown();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_exiting)
            return;
        if (!preferences.Current.CloseToTray)
        {
            Exit();
            return;
        }

        e.Cancel = true;
        _window!.Hide();
        if (!_toldAboutTray)
        {
            _toldAboutTray = true;
            _ = notifier.NotifyAsync("Growth Tracker vẫn đang chạy",
                "Ứng dụng ẩn xuống khay để tiếp tục nhắc bạn. Chọn \"Thoát\" từ biểu tượng khay để tắt hẳn.");
        }
    }
}
