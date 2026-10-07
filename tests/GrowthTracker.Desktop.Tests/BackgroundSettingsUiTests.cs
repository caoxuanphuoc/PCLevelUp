using Avalonia.Headless.XUnit;
using GrowthTracker.Application.Settings;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop.Tests;

public class BackgroundSettingsUiTests
{
    private static async Task<SettingsViewModel> OpenSettingsAsync(TestHost host)
    {
        var (_, vm) = host.OpenMainWindow();
        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(SettingsViewModel));
        await TestHost.WaitForAsync(() => vm.CurrentPage is SettingsViewModel { IsLoading: false, Areas.Count: > 0 });
        return (SettingsViewModel)vm.CurrentPage!;
    }

    [AvaloniaFact]
    public async Task Toggles_ShowDefaults()
    {
        await using var host = await TestHost.CreateAsync();

        var settings = await OpenSettingsAsync(host);

        settings.CloseToTray.Should().BeTrue();
        settings.StartWithOs.Should().BeFalse();
        settings.RemindersEnabled.Should().BeTrue();
        settings.ReminderText.Should().StartWith("Gửi thông báo 10 phút trước giờ ưu tiên");
    }

    [AvaloniaFact]
    public async Task Toggling_SavesSetting_RegistersStartup_AndUpdatesPreferences()
    {
        await using var host = await TestHost.CreateAsync();
        var settings = await OpenSettingsAsync(host);
        var preferences = host.Services.GetRequiredService<AppPreferences>();

        settings.StartWithOs = true;
        await TestHost.WaitForAsync(() => host.Startup.IsRegistered);
        settings.CloseToTray = false;
        await TestHost.WaitForAsync(() => !preferences.Current.CloseToTray);

        var stored = await host.UseCases.QueryAsync(new GetAppSettingsQuery());
        stored.StartWithOs.Should().BeTrue();
        stored.CloseToTray.Should().BeFalse();
        preferences.Current.StartWithOs.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task Refresh_ReloadsCurrentPage()
    {
        await using var host = await TestHost.CreateAsync();
        var settings = await OpenSettingsAsync(host);
        await host.UseCases.SendAsync(new UpdateBackgroundSettingsCommand(RemindersEnabled: false));

        await host.Navigation.RefreshAsync();

        settings.RemindersEnabled.Should().BeFalse();
    }
}
