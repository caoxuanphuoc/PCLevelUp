using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels;
using GrowthTracker.Desktop.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop;

public static class DependencyInjection
{
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TodayViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<GoalsViewModel>();
        services.AddTransient<WeeklyReviewViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }
}
