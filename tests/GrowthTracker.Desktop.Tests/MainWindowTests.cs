using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Desktop.Views.Pages;

namespace GrowthTracker.Desktop.Tests;

public class MainWindowTests
{
    [AvaloniaFact]
    public async Task OpensOnToday_WithSixNavItems()
    {
        await using var host = await TestHost.CreateAsync();
        var (window, vm) = host.OpenMainWindow();

        vm.NavItems.Should().HaveCount(6);
        vm.CurrentPage.Should().BeOfType<TodayViewModel>();
        window.GetVisualDescendants().OfType<TodayView>().Should().ContainSingle();
    }

    [AvaloniaFact]
    public async Task SelectingNavItem_SwapsPageView()
    {
        await using var host = await TestHost.CreateAsync();
        var (window, vm) = host.OpenMainWindow();

        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(SettingsViewModel));
        await TestHost.WaitForAsync(() => window.GetVisualDescendants().OfType<SettingsView>().Any());

        vm.CurrentPage.Should().BeOfType<SettingsViewModel>();
        window.GetVisualDescendants().OfType<TodayView>().Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task ToggleSidebar_CollapsesTo72Pixels()
    {
        await using var host = await TestHost.CreateAsync();
        var (window, vm) = host.OpenMainWindow();
        var sidebar = window.FindControl<Border>("Sidebar")!;
        sidebar.Width.Should().Be(240);

        vm.ToggleSidebarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        sidebar.Width.Should().Be(72);
    }

    [AvaloniaFact]
    public async Task ToggleTheme_SwitchesBetweenLightAndDark()
    {
        await using var host = await TestHost.CreateAsync();
        var (_, vm) = host.OpenMainWindow();
        var app = Avalonia.Application.Current!;
        app.RequestedThemeVariant = ThemeVariant.Light;

        vm.ToggleThemeCommand.Execute(null);
        app.ActualThemeVariant.Should().Be(ThemeVariant.Dark);

        vm.ToggleThemeCommand.Execute(null);
        app.ActualThemeVariant.Should().Be(ThemeVariant.Light);
    }

    [AvaloniaFact]
    public async Task GoBack_RestoresPreviousPage_AndSidebarSelection()
    {
        await using var host = await TestHost.CreateAsync();
        var (_, vm) = host.OpenMainWindow();

        await host.Navigation.NavigateAsync<CalendarViewModel>();
        vm.SelectedNavItem!.PageType.Should().Be(typeof(CalendarViewModel));

        await host.Navigation.GoBackAsync();

        vm.CurrentPage.Should().BeOfType<TodayViewModel>();
        vm.SelectedNavItem!.PageType.Should().Be(typeof(TodayViewModel));
    }

    [AvaloniaFact]
    public async Task PageOutsideSidebar_ClearsSelection_SoSidebarClickNavigatesBack()
    {
        await using var host = await TestHost.CreateAsync();
        var (_, vm) = host.OpenMainWindow();
        var goals = vm.NavItems.Single(i => i.PageType == typeof(GoalsViewModel));
        vm.SelectedNavItem = goals;

        await host.Navigation.NavigateAsync<GoalDetailViewModel>(Guid.NewGuid());
        vm.SelectedNavItem.Should().BeNull();

        vm.SelectedNavItem = goals;
        await TestHost.WaitForAsync(() => vm.CurrentPage is GoalsViewModel);
    }

    [AvaloniaFact]
    public async Task Navigate_ToNonPageType_Throws()
    {
        await using var host = await TestHost.CreateAsync();

        var act = () => host.Navigation.NavigateAsync(typeof(string));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
