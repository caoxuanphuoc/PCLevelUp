using Avalonia.Headless.XUnit;
using GrowthTracker.Application.Settings;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop.Tests;

/// <summary>Sprint 6b: auto-update (TDD §8.3) and the "Đã xảy ra lỗi" dialog (TDD §9.2).</summary>
public class UpdateFlowTests
{
    private static UpdateCoordinator Coordinator(TestHost host) => host.Services.GetRequiredService<UpdateCoordinator>();

    [AvaloniaFact]
    public async Task NewVersion_IsDownloaded_ThenRestartHandsItToTheUpdater()
    {
        await using var host = await TestHost.CreateAsync();
        host.Updates.Available = "1.0.1";
        var (_, vm) = host.OpenMainWindow();
        var updates = vm.Updates;
        bool restartRequested = false;
        updates.RestartRequested += (_, _) => restartRequested = true;

        updates.IsReady.Should().BeFalse();
        await updates.CheckNowCommand.ExecuteAsync(null);

        host.Updates.Downloads.Should().Be(1);
        updates.State.Should().Be(UpdateState.ReadyToRestart);
        updates.IsReady.Should().BeTrue(); // the banner above every page
        updates.BannerText.Should().StartWith("Đã tải xong bản cập nhật 1.0.1");
        updates.CanCheck.Should().BeFalse();

        updates.RestartCommand.Execute(null);
        host.Updates.AppliedOnExit.Should().BeTrue();
        restartRequested.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task UpToDate_AndFailures_AreReported()
    {
        await using var host = await TestHost.CreateAsync();
        var updates = Coordinator(host);

        await updates.CheckAndDownloadAsync(manual: true);
        updates.State.Should().Be(UpdateState.UpToDate);
        updates.StatusText.Should().Be("Bạn đang dùng bản mới nhất.");
        host.Updates.Downloads.Should().Be(0);

        host.Updates.CheckError = new HttpRequestException("offline");
        await updates.CheckAndDownloadAsync(manual: true);
        updates.State.Should().Be(UpdateState.Failed);
        updates.StatusText.Should().StartWith("Không kiểm tra được cập nhật");
        updates.CanCheck.Should().BeTrue(); // can retry
    }

    [AvaloniaFact]
    public async Task DevBuild_CannotUpdate()
    {
        await using var host = await TestHost.CreateAsync();
        host.Updates.IsSupported = false;
        var updates = Coordinator(host);

        updates.State.Should().Be(UpdateState.Unsupported);
        updates.CanCheck.Should().BeFalse();
        updates.StatusText.Should().StartWith("Bản chạy thử");
        updates.VersionText.Should().Be("Phiên bản 1.0.0");
    }

    [AvaloniaFact]
    public async Task Settings_BetaToggle_IsSavedAndUsedForTheNextCheck()
    {
        await using var host = await TestHost.CreateAsync();
        var (_, vm) = host.OpenMainWindow();
        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(SettingsViewModel));
        await TestHost.WaitForAsync(() => vm.CurrentPage is SettingsViewModel { IsLoading: false });
        var settings = (SettingsViewModel)vm.CurrentPage!;

        settings.AutoCheckUpdates.Should().BeTrue();
        settings.IncludePrerelease.Should().BeFalse();
        settings.IncludePrerelease = true;
        settings.AutoCheckUpdates = false;
        await TestHost.WaitForAsync(() => host.Services.GetRequiredService<AppPreferences>().Current is { IncludePrerelease: true, AutoCheckUpdates: false });

        var stored = await host.UseCases.QueryAsync(new GetAppSettingsQuery());
        stored.IncludePrerelease.Should().BeTrue();
        stored.AutoCheckUpdates.Should().BeFalse();

        await settings.Updates.CheckNowCommand.ExecuteAsync(null);
        host.Updates.LastIncludePrerelease.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task UnhandledError_ShowsOneDialog_WithTheLogFolder()
    {
        await using var host = await TestHost.CreateAsync();
        host.OpenMainWindow();

        GlobalExceptionHandlers.ShowErrorDialog(host.Services);
        GlobalExceptionHandlers.ShowErrorDialog(host.Services); // a burst of errors: still one dialog
        var dialog = await host.WaitForDialogAsync<ErrorDialogViewModel>();

        dialog.Title.Should().Be("Đã xảy ra lỗi");
        dialog.LogDirectory.Should().EndWith("logs");
        dialog.DismissCommand.Execute(null);
        host.Dialogs.Current.Should().BeNull();

        string? opened = null;
        new ErrorDialogViewModel("/tmp/logs", path => opened = path).OpenLogsCommand.Execute(null);
        opened.Should().Be("/tmp/logs");
    }
}
