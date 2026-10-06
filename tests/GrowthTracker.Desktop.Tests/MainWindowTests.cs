using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Desktop.Views;
using GrowthTracker.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop.Tests;

public class MainWindowTests
{
    private static (MainWindow Window, MainWindowViewModel Vm, ServiceProvider Services) Open()
    {
        var services = new ServiceCollection().AddDesktop().BuildServiceProvider();
        var vm = services.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, vm, services);
    }

    [AvaloniaFact]
    public void OpensOnToday_WithSixNavItems()
    {
        var (window, vm, _) = Open();

        vm.NavItems.Should().HaveCount(6);
        vm.CurrentPage.Should().BeOfType<TodayViewModel>();
        window.GetVisualDescendants().OfType<TodayView>().Should().ContainSingle();
    }

    [AvaloniaFact]
    public void SelectingNavItem_SwapsPageView()
    {
        var (window, vm, _) = Open();

        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(GoalsViewModel));
        Dispatcher.UIThread.RunJobs();

        vm.CurrentPage.Should().BeOfType<GoalsViewModel>();
        window.GetVisualDescendants().OfType<GoalsView>().Should().ContainSingle();
        window.GetVisualDescendants().OfType<TodayView>().Should().BeEmpty();
    }

    [AvaloniaFact]
    public void ToggleSidebar_CollapsesTo72Pixels()
    {
        var (window, vm, _) = Open();
        var sidebar = window.FindControl<Border>("Sidebar")!;
        sidebar.Width.Should().Be(240);

        vm.ToggleSidebarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        sidebar.Width.Should().Be(72);
    }

    [AvaloniaFact]
    public void ToggleTheme_SwitchesBetweenLightAndDark()
    {
        var (_, vm, _) = Open();
        var app = Avalonia.Application.Current!;
        app.RequestedThemeVariant = ThemeVariant.Light;

        vm.ToggleThemeCommand.Execute(null);
        app.ActualThemeVariant.Should().Be(ThemeVariant.Dark);

        vm.ToggleThemeCommand.Execute(null);
        app.ActualThemeVariant.Should().Be(ThemeVariant.Light);
    }

    [AvaloniaFact]
    public void GoBack_RestoresPreviousPage_AndSidebarSelection()
    {
        var (_, vm, services) = Open();
        var navigation = services.GetRequiredService<INavigationService>();

        navigation.Navigate<CalendarViewModel>();
        vm.SelectedNavItem!.PageType.Should().Be(typeof(CalendarViewModel));

        navigation.GoBack();

        vm.CurrentPage.Should().BeOfType<TodayViewModel>();
        vm.SelectedNavItem!.PageType.Should().Be(typeof(TodayViewModel));
    }

    [AvaloniaFact]
    public void Navigate_ToNonPageType_Throws()
    {
        var (_, _, services) = Open();

        var act = () => services.GetRequiredService<INavigationService>().Navigate(typeof(string));

        act.Should().Throw<ArgumentException>();
    }
}
