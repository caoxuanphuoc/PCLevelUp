using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Goals;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.Plans;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels.Goals;

/// <summary>A Plan row inside a Milestone (or the "no milestone" group) on Goal Detail.</summary>
public sealed partial class PlanItemViewModel(PlanSummaryDto dto, Func<PlanItemViewModel, Task> edit, Func<PlanItemViewModel, Task> archive)
{
    public PlanSummaryDto Dto => dto;
    public Guid Id => dto.Id;
    public string Name => dto.Name;
    public string TypeLabel => Display.PlanType(dto.PlanType).Label;
    public MaterialIconKind TypeIcon => Display.PlanType(dto.PlanType).Icon;

    public string StatusLabel => Display.PlanStatus(dto.Status, dto.HasEnded).Label;
    public string StatusColorHex => Display.PlanStatus(dto.Status, dto.HasEnded).ColorHex;
    public MaterialIconKind StatusIcon => Display.PlanStatus(dto.Status, dto.HasEnded).Icon;
    /// <summary>Active plans can be edited (an ended one too: extending it brings it back) or archived.</summary>
    public bool IsActive => dto.Status == PlanStatus.Active;
    /// <summary>Active and still within its Duration; ended and archived plans are dimmed.</summary>
    public bool IsRunning => IsActive && !dto.HasEnded;

    public string TargetText => Display.Target(dto.PlanType, dto.TargetValue, dto.TargetUnit, dto.FrequencyPerWeek, dto.CriteriaTotal);

    /// <summary>"T2, T4, T6 · 07:30 · Khó".</summary>
    public string ScheduleText => string.Join(" · ", new[]
    {
        Display.Days(dto.ScheduledDays),
        dto.PreferredTime?.ToString("HH:mm"),
        Display.Difficulty(dto.Difficulty),
    }.Where(s => !string.IsNullOrEmpty(s)));

    public string DurationText => Display.DateRange(dto.DurationStart, dto.DurationEnd);

    public string NextSessionText => dto.NextSession is { } next ? Display.Date(next) : "—";
    public string UpcomingText => dto.HasEnded ? $"Kết thúc {Display.Date(dto.DurationEnd)}" : $"{dto.UpcomingSessions} buổi sắp tới";

    public string ScoreText => dto.AverageScore is { } s ? Math.Round(s, MidpointRounding.AwayFromZero).ToString("0") : "—";
    public IBrush ScoreBrush => Display.Brush(dto.AverageScore is { } s ? Display.ScoreTier(s).ColorHex : "#94A3B8");
    public string DoneText => $"{dto.SessionsDone}/{dto.SessionsDue}";
    /// <summary>FR-51: Consistency below 50% over 14 days.</summary>
    public bool MayBeTooHard => dto.MayBeTooHard;
    public string TooHardText => $"Có thể quá khó: chỉ đạt {Display.Number(dto.Consistency14Days ?? 0)}% số buổi trong 14 ngày. Cân nhắc giảm target ở Weekly Review.";
    public bool IsShared => dto.LinkedGoalCount > 1;
    public string SharedText => $"Đóng góp cho {dto.LinkedGoalCount} mục tiêu";

    [RelayCommand]
    private Task Edit() => edit(this);

    [RelayCommand]
    private Task Archive() => archive(this);
}

public sealed partial class MilestoneItemViewModel : ObservableObject
{
    private readonly Func<MilestoneItemViewModel, Task> _remove;
    private readonly Func<MilestoneItemViewModel, Task> _addPlan;

    public MilestoneItemViewModel(MilestoneDto dto, int index, IReadOnlyList<PlanItemViewModel> plans,
                                  Func<MilestoneItemViewModel, Task> addPlan, Func<MilestoneItemViewModel, Task> remove)
    {
        Dto = dto;
        Index = index;
        Plans = plans;
        _addPlan = addPlan;
        _remove = remove;
        IsExpanded = dto.Status != MilestoneStatus.Completed;
    }

    public MilestoneDto Dto { get; }
    public Guid Id => Dto.Id;
    public int Index { get; }
    public string Name => $"Milestone {Index}: {Dto.Name}";
    public IReadOnlyList<PlanItemViewModel> Plans { get; }
    public bool HasPlans => Plans.Count > 0;

    public bool IsCompleted => Dto.Status == MilestoneStatus.Completed;
    /// <summary>Past its due date and not completed.</summary>
    public bool IsOverdue => Dto.IsOverdue;
    public string SubText => string.Join(" • ", new[]
    {
        Dto.DueDate is { } due ? $"Hạn {Display.Date(due)}" : null,
        $"{Plans.Count} kế hoạch",
        Dto.Weight != 1m ? $"Trọng số {Display.Number(Dto.Weight)}" : null,
    }.Where(s => s is not null));

    public double Progress => (double)Dto.Progress;
    public string StatusText => IsCompleted ? "Completed 100%"
                              : IsOverdue ? $"Quá hạn {Display.Percent(Dto.Progress)}"
                              : Dto.Progress > 0 ? $"Đang thực hiện {Display.Percent(Dto.Progress)}"
                              : "Chưa bắt đầu 0%";
    public string StatusColorHex => IsCompleted ? "#15803D" : IsOverdue ? "#D97706" : Dto.Progress > 0 ? "#2563EB" : "#6B7280";
    public MaterialIconKind Icon => IsCompleted ? MaterialIconKind.Star
                                  : IsOverdue ? MaterialIconKind.ClockAlertOutline
                                  : Dto.Progress > 0 ? MaterialIconKind.Autorenew : MaterialIconKind.FlagOutline;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private Task AddPlan() => _addPlan(this);

    [RelayCommand]
    private Task Remove() => _remove(this);
}
