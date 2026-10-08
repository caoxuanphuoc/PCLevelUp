using GrowthTracker.Application.Abstractions;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.Services.Tray;
using GrowthTracker.Desktop.ViewModels;
using GrowthTracker.Desktop.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop;

public static class DependencyInjection
{
    /// <param name="withTray">False in headless tests, where no OS tray exists.</param>
    public static IServiceCollection AddDesktop(this IServiceCollection services, bool withTray = false)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<AppPreferences>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<AppShell>();
        services.AddSingleton<UpdateCoordinator>();

        if (withTray && OperatingSystem.IsWindows())
            AddWindowsTray(services);
        else if (withTray)
        {
            services.AddSingleton<ITrayService, AvaloniaTrayService>();
        }
        else
        {
            services.AddSingleton<ITrayService, NoTrayService>();
        }

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TodayViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<GoalsViewModel>();
        services.AddTransient<GoalDetailViewModel>();
        services.AddTransient<WeeklyReviewViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }

    /// <summary>One Win32 notify icon is both the tray icon and the source of reminder notifications.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void AddWindowsTray(IServiceCollection services)
    {
        services.AddSingleton<WindowsTrayIcon>();
        services.AddSingleton<ITrayService>(sp => sp.GetRequiredService<WindowsTrayIcon>());
        services.AddSingleton<INotifier>(sp => new TrayNotifier(sp.GetRequiredService<WindowsTrayIcon>()));
    }
}
