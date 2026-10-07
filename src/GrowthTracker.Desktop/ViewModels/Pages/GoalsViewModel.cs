using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Goals;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Goals;
using GrowthTracker.Domain.Goals;

namespace GrowthTracker.Desktop.ViewModels.Pages;

/// <summary>An Area tab; AreaId null means "all".</summary>
public sealed partial class AreaFilterItem(Guid? areaId, string name, string colorHex, int count) : ObservableObject
{
    public Guid? AreaId => areaId;
    public string Label => $"{name} ({count})";
    public string ColorHex => colorHex;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed record StatusFilterOption(string Label, IReadOnlyCollection<GoalStatus>? Statuses);

/// <summary>Goals list: summary, Area tabs, status filter and Goal cards (UI Design §6.1).</summary>
public sealed partial class GoalsViewModel(IUseCaseDispatcher dispatcher, IDialogService dialogs, INavigationService navigation,
                                           IClock clock)
    : PageViewModel
{
    public override string Title => "Mục tiêu phát triển";

    public static IReadOnlyList<StatusFilterOption> StatusOptions { get; } =
    [
        new("Đang theo đuổi", null),
        new("At Risk", [GoalStatus.AtRisk]),
        new("On Track", [GoalStatus.OnTrack]),
        new("Hoàn thành", [GoalStatus.Completed]),
        new("Đã huỷ", [GoalStatus.Cancelled]),
        new("Tất cả trạng thái", Enum.GetValues<GoalStatus>()),
    ];

    public ObservableCollection<AreaFilterItem> AreaFilters { get; } = [];
    public ObservableCollection<GoalCardViewModel> Goals { get; } = [];

    [ObservableProperty]
    public partial GoalsSummaryDto? Summary { get; private set; }

    [ObservableProperty]
    public partial StatusFilterOption SelectedStatus { get; set; } = StatusOptions[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasGoals))]
    public partial int GoalCount { get; private set; }

    public bool IsEmpty => !IsLoading && GoalCount == 0;
    public bool HasGoals => GoalCount > 0;

    private Guid? _selectedAreaId;

    public override Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    partial void OnSelectedStatusChanged(StatusFilterOption value) => _ = ReloadAsync();

    [RelayCommand]
    private Task SelectArea(AreaFilterItem item)
    {
        _selectedAreaId = item.AreaId;
        return ReloadAsync();
    }

    [RelayCommand]
    private async Task CreateGoal()
    {
        var goalId = await dialogs.ShowAsync(await GoalEditorViewModel.ForCreateAsync(dispatcher, clock.Today, _selectedAreaId));
        if (goalId is Guid id)
            await navigation.NavigateAsync<GoalDetailViewModel>(id);
    }

    public async Task ReloadAsync()
    {
        await LoadSafelyAsync(LoadAsync);
        OnPropertyChanged(nameof(IsEmpty)); // IsEmpty depends on IsLoading, which only settles after the load
    }

    private async Task LoadAsync()
    {
        var page = await dispatcher.QueryAsync(new GetGoalsQuery(_selectedAreaId, SelectedStatus.Statuses));
        Summary = page.Summary;

        AreaFilters.Clear();
        var all = new AreaFilterItem(null, "Tất cả", "#2563EB", page.AreaFilters.Sum(a => a.GoalCount));
        AreaFilters.Add(all);
        foreach (var area in page.AreaFilters)
            AreaFilters.Add(new AreaFilterItem(area.AreaId, area.Name, area.ColorHex, area.GoalCount));
        if (_selectedAreaId is not null && AreaFilters.All(a => a.AreaId != _selectedAreaId))
            _selectedAreaId = null;
        foreach (var item in AreaFilters)
            item.IsSelected = item.AreaId == _selectedAreaId;

        Goals.Clear();
        foreach (var goal in page.Goals)
            Goals.Add(new GoalCardViewModel(goal, card => navigation.NavigateAsync<GoalDetailViewModel>(card.Id)));
        GoalCount = Goals.Count;
    }
}
