using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Pages;

namespace GrowthTracker.Desktop.ViewModels;

public sealed record NavItem(string Title, string Glyph, Type PageType);

public sealed partial class MainWindowViewModel : ViewModelBase
{
    public const double ExpandedSidebarWidth = 240;
    public const double CollapsedSidebarWidth = 72;

    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;

    public MainWindowViewModel(INavigationService navigation, IThemeService theme)
    {
        _navigation = navigation;
        _theme = theme;
        _navigation.CurrentChanged += (_, _) => OnCurrentPageChanged();

        // UI Design §3.3: the app opens on Today.
        SelectedNavItem = NavItems.First(i => i.PageType == typeof(TodayViewModel));
    }

    public ObservableCollection<NavItem> NavItems { get; } =
    [
        new("Dashboard", "▦", typeof(DashboardViewModel)),
        new("Hôm nay", "☀", typeof(TodayViewModel)),
        new("Lịch biểu", "▤", typeof(CalendarViewModel)),
        new("Goals", "◎", typeof(GoalsViewModel)),
        new("Review", "↻", typeof(WeeklyReviewViewModel)),
        new("Cài đặt", "⚙", typeof(SettingsViewModel)),
    ];

    public PageViewModel? CurrentPage => _navigation.Current;

    [ObservableProperty]
    public partial NavItem? SelectedNavItem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    public partial bool IsSidebarCollapsed { get; set; }

    public double SidebarWidth => IsSidebarCollapsed ? CollapsedSidebarWidth : ExpandedSidebarWidth;

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is not null)
            _navigation.Navigate(value.PageType);
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void ToggleTheme() => _theme.Toggle();

    private void OnCurrentPageChanged()
    {
        OnPropertyChanged(nameof(CurrentPage));
        // Keep the sidebar selection in sync when navigation happens from elsewhere (e.g. Back).
        var item = NavItems.FirstOrDefault(i => i.PageType == CurrentPage?.GetType());
        if (item is not null && item != SelectedNavItem)
            SelectedNavItem = item;
    }
}
