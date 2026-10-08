using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Pages;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels;

public sealed record NavItem(string Title, MaterialIconKind Icon, Type PageType);

public sealed partial class MainWindowViewModel : ViewModelBase
{
    public const double ExpandedSidebarWidth = 240;
    public const double CollapsedSidebarWidth = 72;

    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private readonly IDialogService _dialogs;
    private bool _syncingSelection;

    public MainWindowViewModel(INavigationService navigation, IThemeService theme, IDialogService dialogs, UpdateCoordinator updates)
    {
        _navigation = navigation;
        _theme = theme;
        _dialogs = dialogs;
        Updates = updates;
        _navigation.CurrentChanged += (_, _) => OnCurrentPageChanged();
        _dialogs.CurrentChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentDialog));
            OnPropertyChanged(nameof(HasDialog));
        };

        // UI Design §3.3: the app opens on Today.
        SelectedNavItem = NavItems.First(i => i.PageType == typeof(TodayViewModel));
    }

    public ObservableCollection<NavItem> NavItems { get; } =
    [
        new("Tổng quan", MaterialIconKind.ViewDashboardOutline, typeof(DashboardViewModel)),
        new("Hôm nay", MaterialIconKind.WeatherSunny, typeof(TodayViewModel)),
        new("Lịch biểu", MaterialIconKind.CalendarMonthOutline, typeof(CalendarViewModel)),
        new("Mục tiêu", MaterialIconKind.FlagOutline, typeof(GoalsViewModel)),
        new("Đánh giá tuần", MaterialIconKind.ChartBoxOutline, typeof(WeeklyReviewViewModel)),
        new("Cài đặt", MaterialIconKind.CogOutline, typeof(SettingsViewModel)),
    ];

    /// <summary>Drives the "update downloaded — restart" banner above every page.</summary>
    public UpdateCoordinator Updates { get; }

    public PageViewModel? CurrentPage => _navigation.Current;

    public DialogViewModelBase? CurrentDialog => _dialogs.Current;
    public bool HasDialog => _dialogs.Current is not null;

    [ObservableProperty]
    public partial NavItem? SelectedNavItem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    public partial bool IsSidebarCollapsed { get; set; }

    public double SidebarWidth => IsSidebarCollapsed ? CollapsedSidebarWidth : ExpandedSidebarWidth;

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is not null && !_syncingSelection)
            _ = _navigation.NavigateAsync(value.PageType);
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void ToggleTheme() => _theme.Toggle();

    /// <summary>Escape closes the top dialog without a result.</summary>
    [RelayCommand]
    private void CloseDialog() => _dialogs.Current?.Cancel();

    /// <summary>
    /// Keeps the sidebar in sync with navigation from elsewhere (Back, opening a Goal). Pages outside the sidebar
    /// (Goal Detail) clear the selection, so clicking "Mục tiêu" navigates back to the list.
    /// </summary>
    private void OnCurrentPageChanged()
    {
        OnPropertyChanged(nameof(CurrentPage));
        var item = NavItems.FirstOrDefault(i => i.PageType == CurrentPage?.GetType());
        if (item == SelectedNavItem)
            return;
        _syncingSelection = true;
        try
        {
            SelectedNavItem = item;
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}
