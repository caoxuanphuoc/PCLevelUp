using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Plans;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Goals;
using GrowthTracker.Domain.Goals;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels.Pages;

public enum GoalDetailTab
{
    Milestones,
    Plans,
}

/// <summary>Goal Detail: header, risk banner, figures, and the Milestone → Plan tree (UI Design §6.2).</summary>
public sealed partial class GoalDetailViewModel(IUseCaseDispatcher dispatcher, IDialogService dialogs,
                                                INavigationService navigation, IClock clock) : PageViewModel
{
    public override string Title => Detail?.Name ?? "Chi tiết mục tiêu";

    public ObservableCollection<MilestoneItemViewModel> Milestones { get; } = [];
    public ObservableCollection<PlanItemViewModel> UnassignedPlans { get; } = [];
    public ObservableCollection<PlanItemViewModel> AllPlans { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HasDetail), nameof(AreaName), nameof(AreaBrush), nameof(AreaColorHex),
                              nameof(StatusLabel), nameof(StatusColorHex), nameof(StatusIcon), nameof(IsAtRisk), nameof(IsClosed),
                              nameof(PriorityLabel), nameof(PriorityColorHex), nameof(DateRange), nameof(DaysRemainingText), nameof(DaysRemaining), nameof(DeadlineText),
                              nameof(Progress), nameof(Expected), nameof(ProgressText), nameof(ExpectedText), nameof(GapText),
                              nameof(GapBrush), nameof(ScoreText), nameof(ScoreLabel), nameof(ScoreBrush), nameof(RiskMessage),
                              nameof(MilestonesTabText), nameof(PlansTabText), nameof(Description), nameof(TargetDescription),
                              nameof(HasTargetDescription), nameof(IsTreeEmpty))]
    public partial GoalDetailDto? Detail { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMilestonesTab), nameof(IsPlansTab))]
    public partial GoalDetailTab SelectedTab { get; set; } = GoalDetailTab.Milestones;

    public bool IsMilestonesTab => SelectedTab == GoalDetailTab.Milestones;
    public bool IsPlansTab => SelectedTab == GoalDetailTab.Plans;

    public bool HasDetail => Detail is not null;
    public string AreaName => Detail?.AreaName ?? "";
    public string AreaColorHex => Detail?.AreaColorHex ?? "#6B7280";
    public IBrush AreaBrush => Display.Brush(AreaColorHex);
    public string? Description => Detail?.Description;
    public string? TargetDescription => Detail?.TargetDescription;
    public bool HasTargetDescription => !string.IsNullOrWhiteSpace(Detail?.TargetDescription);

    private StatusLook Status => Display.GoalStatus(Detail?.Status ?? GoalStatus.Draft);
    public string StatusLabel => Status.Label.ToUpperInvariant();
    public string StatusColorHex => Status.ColorHex;
    public MaterialIconKind StatusIcon => Status.Icon;
    public bool IsAtRisk => Detail?.Status == GoalStatus.AtRisk;
    public bool IsClosed => Detail?.Status is GoalStatus.Completed or GoalStatus.Cancelled;

    public string PriorityLabel => Display.Priority(Detail?.Priority ?? 4).Label.ToUpperInvariant();
    public string PriorityColorHex => Display.Priority(Detail?.Priority ?? 4).ColorHex;
    public string DateRange => Detail is null ? "" : Display.DateRange(Detail.StartDate, Detail.TargetDate);
    public string DaysRemainingText => Detail is null ? "" : $"Còn {Detail.DaysRemaining} ngày";
    public int DaysRemaining => Detail?.DaysRemaining ?? 0;
    public string DeadlineText => Detail is null ? "" : $"Hạn chót: {Display.Date(Detail.TargetDate)}";

    public double Progress => (double)(Detail?.Progress ?? 0);
    public double Expected => (double)(Detail?.ExpectedProgress ?? 0);
    public string ProgressText => Display.Percent(Detail?.Progress ?? 0);
    public string ExpectedText => Display.Percent(Detail?.ExpectedProgress ?? 0);
    private decimal Gap => Math.Round((Detail?.Progress ?? 0) - (Detail?.ExpectedProgress ?? 0), MidpointRounding.AwayFromZero);
    public string GapText => Gap < 0 ? $"↓ Chậm {-Gap:0}%" : Gap > 0 ? $"↑ Vượt {Gap:0}%" : "→ Đúng nhịp";
    public IBrush GapBrush => Display.Brush(Gap < -10 ? "#DC2626" : Gap < 0 ? "#D97706" : "#16A34A");

    public string ScoreText => Detail?.Score is { } s ? Math.Round(s, MidpointRounding.AwayFromZero).ToString("0") : "—";
    public string ScoreLabel => Detail?.Score is { } s ? $"Điểm {Display.ScoreTier(s).Label}" : "Chưa có Plan Log";
    public IBrush ScoreBrush => Display.Brush(Detail?.Score is { } s ? Display.ScoreTier(s).ColorHex : "#94A3B8");

    /// <summary>FR-50 explanation shown in the At Risk banner; it always comes with actions (UI Design §1, rule 5).</summary>
    public string RiskMessage => Detail is null ? ""
        : $"Tiến độ hiện tại {ProgressText}, thấp hơn kỳ vọng {ExpectedText}. Hãy xem lại khối lượng các kế hoạch hoặc dời deadline.";

    public string MilestonesTabText => $"Milestones ({Milestones.Count})";
    public string PlansTabText => $"Plans ({AllPlans.Count})";
    public bool IsTreeEmpty => Milestones.Count == 0 && UnassignedPlans.Count == 0;
    public bool HasUnassignedPlans => UnassignedPlans.Count > 0;

    private Guid _goalId;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is Guid id)
            _goalId = id;
        return ReloadAsync();
    }

    public Task ReloadAsync() => LoadSafelyAsync(async () =>
    {
        var detail = await dispatcher.QueryAsync(new GetGoalDetailQuery(_goalId));
        if (detail is null)
        {
            LoadError = "Mục tiêu không còn tồn tại.";
            Detail = null;
            return;
        }

        PlanItemViewModel Item(PlanSummaryDto p) => new(p, EditPlanAsync, ArchivePlanAsync);

        Milestones.Clear();
        int index = 1;
        foreach (var m in detail.Milestones)
            Milestones.Add(new MilestoneItemViewModel(m, index++, m.Plans.Select(Item).ToList(), AddPlanToMilestoneAsync, RemoveMilestoneAsync));
        UnassignedPlans.Clear();
        foreach (var p in detail.UnassignedPlans)
            UnassignedPlans.Add(Item(p));
        AllPlans.Clear();
        foreach (var p in Milestones.SelectMany(m => m.Plans).Concat(UnassignedPlans))
            AllPlans.Add(p);

        Detail = detail;
        OnPropertyChanged(nameof(HasUnassignedPlans));
    });

    [RelayCommand]
    private Task Back() => navigation.CanGoBack ? navigation.GoBackAsync() : navigation.NavigateAsync<GoalsViewModel>();

    [RelayCommand]
    private void ShowTab(GoalDetailTab tab) => SelectedTab = tab;

    [RelayCommand]
    private async Task EditGoal()
    {
        if (Detail is null)
            return;
        if (await dialogs.ShowAsync(await GoalEditorViewModel.ForEditAsync(dispatcher, Detail)) is not null)
            await ReloadAsync();
    }

    [RelayCommand]
    private async Task CancelGoal()
    {
        if (Detail is null)
            return;
        bool confirmed = await dialogs.ConfirmAsync(
            "Huỷ mục tiêu?",
            "Các kế hoạch chỉ phục vụ mục tiêu này sẽ được lưu trữ và các buổi sắp tới bị xoá khỏi lịch. XP đã nhận vẫn được giữ.",
            "Huỷ mục tiêu", destructive: true);
        if (!confirmed)
            return;
        await dispatcher.SendAsync(new CancelGoalCommand(Detail.Id));
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task AddMilestone()
    {
        if (Detail is null)
            return;
        if (await dialogs.ShowAsync(new MilestoneEditorViewModel(dispatcher, Detail.Id, Detail.Name)) is not null)
            await ReloadAsync();
    }

    [RelayCommand]
    private Task AddPlan() => AddPlanAsync(null);

    private Task AddPlanToMilestoneAsync(MilestoneItemViewModel milestone) => AddPlanAsync(milestone.Id);

    private async Task AddPlanAsync(Guid? milestoneId)
    {
        if (Detail is null)
            return;
        var result = await dialogs.ShowAsync(new PlanEditorViewModel(dispatcher, Detail, milestoneId, clock.Today));
        if (result is not null)
            await ReloadAsync();
    }

    private async Task EditPlanAsync(PlanItemViewModel plan)
    {
        if (Detail is null)
            return;
        if (await dialogs.ShowAsync(PlanEditorViewModel.ForEdit(dispatcher, Detail, plan.Dto, clock.Today)) is not null)
            await ReloadAsync();
    }

    private async Task RemoveMilestoneAsync(MilestoneItemViewModel milestone)
    {
        bool confirmed = await dialogs.ConfirmAsync(
            "Xoá milestone?",
            $"\"{milestone.Dto.Name}\" sẽ bị xoá. Các kế hoạch bên trong vẫn chạy và chuyển về mục \"Không thuộc milestone\".",
            "Xoá milestone", destructive: true);
        if (!confirmed)
            return;
        await dispatcher.SendAsync(new RemoveMilestoneCommand(milestone.Id));
        await ReloadAsync();
    }

    private async Task ArchivePlanAsync(PlanItemViewModel plan)
    {
        bool confirmed = await dialogs.ConfirmAsync(
            "Lưu trữ kế hoạch?",
            $"\"{plan.Name}\" sẽ ngừng lên lịch và các buổi sắp tới bị xoá. Lịch sử và XP đã nhận vẫn được giữ.",
            "Lưu trữ");
        if (!confirmed)
            return;
        await dispatcher.SendAsync(new ArchivePlanCommand(plan.Id));
        await ReloadAsync();
    }
}
