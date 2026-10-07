using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Plans;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scheduling;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

public sealed record PlanTypeOption(PlanType Value, string Label, string Hint, MaterialIconKind Icon);

public sealed record DifficultyOption(Difficulty Value, string Label);

/// <summary>Null Id = "not in a milestone".</summary>
public sealed record MilestoneOption(Guid? Id, string Name);

public sealed partial class WeekDayToggle(WeekDays day, string label, Action changed) : ObservableObject
{
    public WeekDays Day => day;
    public string Label => label;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    partial void OnIsCheckedChanged(bool value) => changed();
}

/// <summary>
/// Creates a Plan for a Goal (FR-04). The main inputs change with the plan type (BRD §5.2), and a live preview
/// shows how many sessions will be scheduled. Returns the result, or null when cancelled.
/// </summary>
public sealed partial class PlanEditorViewModel : DialogViewModel<CreatePlanResult?>
{
    private readonly IUseCaseDispatcher _dispatcher;
    private readonly Guid _goalId;
    private readonly DateOnly _today;

    public PlanEditorViewModel(IUseCaseDispatcher dispatcher, GoalDetailDto goal, Guid? milestoneId, DateOnly today)
    {
        _dispatcher = dispatcher;
        _goalId = goal.Id;
        _today = today;
        GoalName = goal.Name;
        Milestones = [new MilestoneOption(null, "Không thuộc milestone"), .. goal.Milestones.Select(m => new MilestoneOption(m.Id, m.Name))];
        SelectedMilestone = Milestones.FirstOrDefault(m => m.Id == milestoneId) ?? Milestones[0];
        DayToggles = Display.WeekDayLabels.Select(d => new WeekDayToggle(d.Day, d.Label, UpdatePreview)).ToList();
        SelectedType = PlanTypes[0];
        SelectedDifficulty = Difficulties[1];

        var start = goal.StartDate > today ? goal.StartDate : today;
        DurationStart = start.ToDateTime(TimeOnly.MinValue);
        DurationEnd = (goal.TargetDate > start ? goal.TargetDate : start.AddMonths(3)).ToDateTime(TimeOnly.MinValue);
        foreach (var toggle in DayToggles.Where(t => t.Day is WeekDays.Monday or WeekDays.Wednesday or WeekDays.Friday))
            toggle.IsChecked = true;
    }

    public override string Title => "Thêm kế hoạch";
    public string GoalName { get; }

    public static IReadOnlyList<PlanTypeOption> PlanTypes { get; } =
    [
        new(PlanType.Time, "Thời gian", "Số phút mỗi buổi", MaterialIconKind.TimerOutline),
        new(PlanType.Quantity, "Số lượng", "Số lượng mỗi buổi", MaterialIconKind.Counter),
        new(PlanType.Frequency, "Tần suất", "Số buổi mỗi tuần", MaterialIconKind.RepeatVariant),
        new(PlanType.Binary, "Có / Không", "Làm hay không làm", MaterialIconKind.ToggleSwitchOutline),
        new(PlanType.Achievement, "Thành tựu", "Danh sách tiêu chí", MaterialIconKind.TrophyOutline),
    ];

    public static IReadOnlyList<DifficultyOption> Difficulties { get; } =
        Enum.GetValues<Difficulty>().Select(d => new DifficultyOption(d, Display.Difficulty(d))).ToList();

    public IReadOnlyList<MilestoneOption> Milestones { get; }
    public IReadOnlyList<WeekDayToggle> DayToggles { get; }
    public ObservableCollection<string> Criteria { get; } = [];

    [ObservableProperty] public partial string? Name { get; set; }
    [ObservableProperty] public partial MilestoneOption SelectedMilestone { get; set; }
    [ObservableProperty] public partial PlanTypeOption SelectedType { get; set; }
    [ObservableProperty] public partial decimal? TargetValue { get; set; } = 30m;
    [ObservableProperty] public partial string? TargetUnit { get; set; } = "phút";
    [ObservableProperty] public partial decimal? FrequencyPerWeek { get; set; } = 3m;
    [ObservableProperty] public partial bool UseSpecificDays { get; set; } = true;
    [ObservableProperty] public partial TimeSpan? PreferredTime { get; set; }
    [ObservableProperty] public partial DateTime? DurationStart { get; set; }
    [ObservableProperty] public partial DateTime? DurationEnd { get; set; }
    [ObservableProperty] public partial DifficultyOption SelectedDifficulty { get; set; }
    [ObservableProperty] public partial string? NewCriterion { get; set; }
    [ObservableProperty] public partial string PreviewText { get; private set; } = "";

    public bool ShowsTarget => SelectedType.Value is PlanType.Time or PlanType.Quantity;
    public bool ShowsFrequency => SelectedType.Value == PlanType.Frequency;
    public bool ShowsCriteria => SelectedType.Value == PlanType.Achievement;
    /// <summary>Frequency plans may leave the days open (spread across the week automatically).</summary>
    public bool ShowsDayToggles => !ShowsFrequency || UseSpecificDays;

    partial void OnSelectedTypeChanged(PlanTypeOption value)
    {
        TargetUnit = value.Value switch
        {
            PlanType.Time => "phút",
            PlanType.Quantity => "lần",
            _ => TargetUnit,
        };
        if (value.Value == PlanType.Frequency)
            UseSpecificDays = false;
        OnPropertyChanged(nameof(ShowsTarget));
        OnPropertyChanged(nameof(ShowsFrequency));
        OnPropertyChanged(nameof(ShowsCriteria));
        OnPropertyChanged(nameof(ShowsDayToggles));
        UpdatePreview();
    }

    partial void OnUseSpecificDaysChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsDayToggles));
        UpdatePreview();
    }

    partial void OnFrequencyPerWeekChanged(decimal? value) => UpdatePreview();
    partial void OnDurationStartChanged(DateTime? value) => UpdatePreview();
    partial void OnDurationEndChanged(DateTime? value) => UpdatePreview();

    [RelayCommand]
    private void AddCriterion()
    {
        if (string.IsNullOrWhiteSpace(NewCriterion))
            return;
        Criteria.Add(NewCriterion.Trim());
        NewCriterion = null;
    }

    [RelayCommand]
    private void RemoveCriterion(string criterion) => Criteria.Remove(criterion);

    [RelayCommand]
    private Task Submit()
    {
        ErrorMessage = Validate();
        if (ErrorMessage is not null)
            return Task.CompletedTask;

        var type = SelectedType.Value;
        var command = new CreatePlanCommand(
            _goalId, Name!, type,
            TargetValue: type switch
            {
                PlanType.Time or PlanType.Quantity => TargetValue ?? 0,
                PlanType.Frequency => FrequencyPerWeek ?? 0,
                _ => 1,
            },
            TargetUnit: type is PlanType.Time or PlanType.Quantity ? TargetUnit : type == PlanType.Frequency ? "buổi" : null,
            DateOnly.FromDateTime(DurationStart!.Value), DateOnly.FromDateTime(DurationEnd!.Value),
            SelectedDifficulty.Value,
            DaysOfWeek: ShowsDayToggles ? SelectedDays : WeekDays.None,
            FrequencyPerWeek: type == PlanType.Frequency ? (int?)(FrequencyPerWeek ?? 0) : null,
            PreferredTime: PreferredTime is { } t ? TimeOnly.FromTimeSpan(t) : null,
            MilestoneId: SelectedMilestone.Id,
            Criteria: type == PlanType.Achievement ? Criteria.ToList() : null);

        return SubmitAsync(() => _dispatcher.SendAsync(command), r => (CreatePlanResult?)r);
    }

    private WeekDays SelectedDays => DayToggles.Where(t => t.IsChecked).Aggregate(WeekDays.None, (acc, t) => acc | t.Day);

    private string? Validate() =>
        string.IsNullOrWhiteSpace(Name) ? "Hãy nhập tên kế hoạch."
        : DurationStart is null || DurationEnd is null ? "Hãy chọn thời gian thực hiện."
        : DurationEnd < DurationStart ? "Ngày kết thúc phải sau ngày bắt đầu."
        : ShowsTarget && (TargetValue ?? 0) <= 0 ? "Hãy nhập target lớn hơn 0."
        : ShowsFrequency && FrequencyPerWeek is not (>= 1 and <= 7) ? "Chọn từ 1 đến 7 buổi mỗi tuần."
        : ShowsDayToggles && SelectedDays == WeekDays.None ? "Chọn ít nhất một ngày trong tuần."
        : ShowsCriteria && Criteria.Count == 0 ? "Thêm ít nhất một tiêu chí hoàn thành."
        : null;

    /// <summary>Live "N sessions in the next 8 weeks" preview from the same generator the use case runs.</summary>
    private void UpdatePreview()
    {
        if (DurationStart is null || DurationEnd is null || DurationEnd < DurationStart)
        {
            PreviewText = "";
            return;
        }
        var input = new ScheduleInput(SelectedType.Value,
            DateOnly.FromDateTime(DurationStart.Value), DateOnly.FromDateTime(DurationEnd.Value),
            ShowsDayToggles ? SelectedDays : WeekDays.None,
            ShowsFrequency ? (int?)(FrequencyPerWeek ?? 0) : null);
        var sessions = ScheduleGenerator.Generate(input, _today);
        PreviewText = sessions.Count == 0
            ? "Chưa có buổi nào được lên lịch với cài đặt này."
            : $"Sẽ lên lịch {sessions.Count} buổi trong 8 tuần tới ({Display.Days(ScheduleGenerator.EffectiveDays(input))}), " +
              $"buổi đầu {Display.Date(sessions[0])}. Tổng {ScheduleGenerator.CountSessions(input)} buổi đến {Display.Date(input.DurationEnd)}.";
    }
}
