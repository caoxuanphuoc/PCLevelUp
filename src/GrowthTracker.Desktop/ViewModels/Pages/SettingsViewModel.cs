using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Settings;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;

namespace GrowthTracker.Desktop.ViewModels.Pages;

public sealed partial class AreaRowViewModel(AreaDto dto, Func<AreaRowViewModel, Task> edit, Func<AreaRowViewModel, Task> toggleArchive)
{
    public AreaDto Dto => dto;
    public string Name => dto.Name;
    public IBrush ColorBrush => Display.Brush(dto.ColorHex);
    public bool IsArchived => dto.IsArchived;
    public string GoalText => dto.ActiveGoalCount == 0 ? "Chưa có mục tiêu" : $"{dto.ActiveGoalCount} mục tiêu đang theo đuổi";
    public string ArchiveText => dto.IsArchived ? "Khôi phục" : "Lưu trữ";

    [RelayCommand]
    private Task Edit() => edit(this);

    [RelayCommand]
    private Task ToggleArchive() => toggleArchive(this);
}

/// <summary>Settings: Areas (FR-01), running in the background (TDD §7.2) and updates (TDD §8.3). Theme and backup come later.</summary>
public sealed partial class SettingsViewModel(IUseCaseDispatcher dispatcher, IDialogService dialogs, AppPreferences preferences,
                                             UpdateCoordinator updates)
    : PageViewModel
{
    private bool _applying;

    public override string Title => "Cài đặt";

    public ObservableCollection<AreaRowViewModel> Areas { get; } = [];

    [ObservableProperty]
    public partial string? ActionError { get; private set; }

    [ObservableProperty] public partial bool CloseToTray { get; set; }
    [ObservableProperty] public partial bool StartWithOs { get; set; }
    [ObservableProperty] public partial bool RemindersEnabled { get; set; }
    [ObservableProperty] public partial int ReminderLeadMinutes { get; private set; }
    [ObservableProperty] public partial bool AutoCheckUpdates { get; set; }
    [ObservableProperty] public partial bool IncludePrerelease { get; set; }

    public UpdateCoordinator Updates => updates;

    public string ReminderText => $"Gửi thông báo {ReminderLeadMinutes} phút trước giờ ưu tiên của mỗi buổi (khi ứng dụng đang chạy).";

    public override Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    public Task ReloadAsync() => LoadSafelyAsync(async () =>
    {
        var areas = await dispatcher.QueryAsync(new GetAreasQuery(IncludeArchived: true));
        Areas.Clear();
        foreach (var area in areas.OrderBy(a => a.IsArchived).ThenBy(a => a.SortOrder))
            Areas.Add(new AreaRowViewModel(area, EditAsync, ToggleArchiveAsync));
        ShowSettings(await dispatcher.QueryAsync(new GetAppSettingsQuery()));
    });

    partial void OnCloseToTrayChanged(bool value) => _ = ApplyAsync(new UpdateBackgroundSettingsCommand(CloseToTray: value));
    partial void OnStartWithOsChanged(bool value) => _ = ApplyAsync(new UpdateBackgroundSettingsCommand(StartWithOs: value));
    partial void OnRemindersEnabledChanged(bool value) => _ = ApplyAsync(new UpdateBackgroundSettingsCommand(RemindersEnabled: value));
    partial void OnAutoCheckUpdatesChanged(bool value) => _ = ApplyAsync(new UpdateBackgroundSettingsCommand(AutoCheckUpdates: value));
    partial void OnIncludePrereleaseChanged(bool value) => _ = ApplyAsync(new UpdateBackgroundSettingsCommand(IncludePrerelease: value));

    /// <summary>Saves one toggle; on failure the toggles go back to what is actually stored.</summary>
    public async Task ApplyAsync(UpdateBackgroundSettingsCommand command)
    {
        if (_applying)
            return;
        var result = await dispatcher.SendAsync(command);
        ActionError = result.IsSuccess ? null : "Không thể lưu thiết lập: " + result.Error!.Message;
        ShowSettings(result.IsSuccess ? result.Value! : await dispatcher.QueryAsync(new GetAppSettingsQuery()));
    }

    private void ShowSettings(AppSettingsDto settings)
    {
        preferences.Update(settings);
        _applying = true;
        try
        {
            CloseToTray = settings.CloseToTray;
            StartWithOs = settings.StartWithOs;
            RemindersEnabled = settings.RemindersEnabled;
            ReminderLeadMinutes = settings.ReminderLeadMinutes;
            AutoCheckUpdates = settings.AutoCheckUpdates;
            IncludePrerelease = settings.IncludePrerelease;
            OnPropertyChanged(nameof(ReminderText));
        }
        finally
        {
            _applying = false;
        }
    }

    [RelayCommand]
    private async Task AddArea()
    {
        if (await dialogs.ShowAsync(new AreaEditorViewModel(dispatcher)) is not null)
            await ReloadAsync();
    }

    private async Task EditAsync(AreaRowViewModel row)
    {
        if (await dialogs.ShowAsync(new AreaEditorViewModel(dispatcher, row.Dto)) is not null)
            await ReloadAsync();
    }

    private async Task ToggleArchiveAsync(AreaRowViewModel row)
    {
        var result = await dispatcher.SendAsync(new SetAreaArchivedCommand(row.Dto.Id, !row.IsArchived));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        await ReloadAsync();
    }
}
