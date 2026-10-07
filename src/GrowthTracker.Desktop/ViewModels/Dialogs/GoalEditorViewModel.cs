using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Goals;
using GrowthTracker.Desktop.Presentation;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

public sealed record PriorityOption(int Value, string Label);

/// <summary>
/// Create / edit a Goal in two steps (UI Design §6.1): (1) name, Area, description, dates;
/// (2) priority and measurable target. Returns the Goal id, or null when cancelled.
/// </summary>
public sealed partial class GoalEditorViewModel : DialogViewModel<Guid?>
{
    private readonly IUseCaseDispatcher _dispatcher;
    private readonly Guid? _goalId;

    private GoalEditorViewModel(IUseCaseDispatcher dispatcher, IReadOnlyList<AreaDto> areas, Guid? goalId)
    {
        _dispatcher = dispatcher;
        _goalId = goalId;
        Areas = areas;
    }

    public static async Task<GoalEditorViewModel> ForCreateAsync(IUseCaseDispatcher dispatcher, DateOnly today, Guid? preselectedAreaId = null)
    {
        var areas = await dispatcher.QueryAsync(new GetAreasQuery());
        var start = today.ToDateTime(TimeOnly.MinValue);
        return new GoalEditorViewModel(dispatcher, areas, null)
        {
            SelectedArea = areas.FirstOrDefault(a => a.Id == preselectedAreaId) ?? areas.FirstOrDefault(),
            StartDate = start,
            TargetDate = start.AddMonths(6),
            SelectedPriority = PriorityOptions[1],
        };
    }

    public static async Task<GoalEditorViewModel> ForEditAsync(IUseCaseDispatcher dispatcher, GoalDetailDto goal)
    {
        var areas = await dispatcher.QueryAsync(new GetAreasQuery(IncludeArchived: true));
        return new GoalEditorViewModel(dispatcher, areas.Where(a => !a.IsArchived || a.Id == goal.AreaId).ToList(), goal.Id)
        {
            SelectedArea = areas.FirstOrDefault(a => a.Id == goal.AreaId),
            Name = goal.Name,
            Description = goal.Description,
            StartDate = goal.StartDate.ToDateTime(TimeOnly.MinValue),
            TargetDate = goal.TargetDate.ToDateTime(TimeOnly.MinValue),
            SelectedPriority = PriorityOptions.First(p => p.Value == goal.Priority),
            TargetDescription = goal.TargetDescription,
        };
    }

    public bool IsEdit => _goalId is not null;
    public override string Title => IsEdit ? "Sửa mục tiêu" : "Tạo mục tiêu mới";
    public string SubmitText => IsEdit ? "Lưu thay đổi" : "Tạo mục tiêu";

    public IReadOnlyList<AreaDto> Areas { get; }

    public static IReadOnlyList<PriorityOption> PriorityOptions { get; } =
        Enumerable.Range(1, 4).Select(p => new PriorityOption(p, Display.Priority(p).Label)).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStepOne), nameof(IsStepTwo), nameof(StepText))]
    public partial int Step { get; private set; } = 1;

    public bool IsStepOne => Step == 1;
    public bool IsStepTwo => Step == 2;
    public string StepText => $"Bước {Step}/2";

    [ObservableProperty] public partial string? Name { get; set; }
    [ObservableProperty] public partial AreaDto? SelectedArea { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial DateTime? StartDate { get; set; }
    [ObservableProperty] public partial DateTime? TargetDate { get; set; }
    [ObservableProperty] public partial PriorityOption SelectedPriority { get; set; } = PriorityOptions[1];
    [ObservableProperty] public partial string? TargetDescription { get; set; }

    [RelayCommand]
    private void Next()
    {
        ErrorMessage = ValidateStepOne();
        if (ErrorMessage is null)
            Step = 2;
    }

    [RelayCommand]
    private void Back() => Step = 1;

    [RelayCommand]
    private async Task Submit()
    {
        ErrorMessage = ValidateStepOne();
        if (ErrorMessage is not null)
        {
            Step = 1;
            return;
        }

        var fields = new GoalFields(SelectedArea!.Id, Name!, DateOnly.FromDateTime(StartDate!.Value),
                                    DateOnly.FromDateTime(TargetDate!.Value), SelectedPriority.Value,
                                    Description, TargetDescription);
        await SubmitAsync(
            () => _goalId is Guid id
                ? _dispatcher.SendAsync(new UpdateGoalCommand(id, fields))
                : _dispatcher.SendAsync(new CreateGoalCommand(fields)),
            id => (Guid?)id);
    }

    private string? ValidateStepOne() =>
        string.IsNullOrWhiteSpace(Name) ? "Hãy nhập tên mục tiêu."
        : SelectedArea is null ? "Hãy chọn lĩnh vực."
        : StartDate is null || TargetDate is null ? "Hãy chọn ngày bắt đầu và ngày mục tiêu."
        : TargetDate <= StartDate ? "Ngày mục tiêu phải sau ngày bắt đầu."
        : null;
}
