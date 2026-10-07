using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Areas;
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

/// <summary>Settings: manage Areas (FR-01). Theme, backup and restore come in later sprints.</summary>
public sealed partial class SettingsViewModel(IUseCaseDispatcher dispatcher, IDialogService dialogs) : PageViewModel
{
    public override string Title => "Cài đặt";

    public ObservableCollection<AreaRowViewModel> Areas { get; } = [];

    [ObservableProperty]
    public partial string? ActionError { get; private set; }

    public override Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    public Task ReloadAsync() => LoadSafelyAsync(async () =>
    {
        var areas = await dispatcher.QueryAsync(new GetAreasQuery(IncludeArchived: true));
        Areas.Clear();
        foreach (var area in areas.OrderBy(a => a.IsArchived).ThenBy(a => a.SortOrder))
            Areas.Add(new AreaRowViewModel(area, EditAsync, ToggleArchiveAsync));
    });

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
