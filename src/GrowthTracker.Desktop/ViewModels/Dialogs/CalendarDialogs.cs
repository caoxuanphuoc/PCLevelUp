using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Calendar;
using GrowthTracker.Desktop.Presentation;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

/// <summary>
/// Reschedule a session (FR-23): pick the day (pre-filled when dropped on the calendar), "only this one" or
/// "this and the following", and an optional reason. Returns how many sessions moved, or null when cancelled.
/// </summary>
public sealed partial class RescheduleDialogViewModel : DialogViewModel<int?>
{
    private readonly IUseCaseDispatcher _dispatcher;
    private readonly CalendarSessionDto _session;

    public RescheduleDialogViewModel(IUseCaseDispatcher dispatcher, CalendarSessionDto session, DateOnly? target = null)
    {
        _dispatcher = dispatcher;
        _session = session;
        NewDate = (target ?? session.Date).ToDateTime(TimeOnly.MinValue);
        NewTime = session.Time?.ToTimeSpan();
    }

    public override string Title => "Dời lịch";
    public string SessionName => _session.PlanName;
    public string CurrentText => $"Đang ở {Display.Date(_session.Date)}" + (_session.Time is { } t ? $" lúc {t:HH:mm}" : "");

    [ObservableProperty] public partial DateTime? NewDate { get; set; }
    [ObservableProperty] public partial TimeSpan? NewTime { get; set; }
    [ObservableProperty] public partial bool MoveFollowing { get; set; }
    [ObservableProperty] public partial string? Reason { get; set; }

    [RelayCommand]
    private void SetScope(bool following) => MoveFollowing = following;

    [RelayCommand]
    private Task Submit()
    {
        if (NewDate is null)
        {
            ErrorMessage = "Hãy chọn ngày mới.";
            return Task.CompletedTask;
        }
        var command = new RescheduleOccurrenceCommand(
            _session.OccurrenceId, DateOnly.FromDateTime(NewDate.Value),
            MoveFollowing ? RescheduleScope.ThisAndFollowing : RescheduleScope.ThisOnly,
            Reason, NewTime is { } t ? TimeOnly.FromTimeSpan(t) : null);
        return SubmitAsync(() => _dispatcher.SendAsync(command), moved => (int?)moved);
    }
}

/// <summary>Adds one extra session of an active plan to a day (TDD §3.1 AddPlanToCalendar).</summary>
public sealed partial class AddSessionDialogViewModel : DialogViewModel<Guid?>
{
    private readonly IUseCaseDispatcher _dispatcher;

    private AddSessionDialogViewModel(IUseCaseDispatcher dispatcher, IReadOnlyList<SchedulablePlanDto> plans, DateOnly date)
    {
        _dispatcher = dispatcher;
        Plans = plans;
        Date = date.ToDateTime(TimeOnly.MinValue);
        SelectedPlan = plans.FirstOrDefault();
    }

    public static async Task<AddSessionDialogViewModel> CreateAsync(IUseCaseDispatcher dispatcher, DateOnly date) =>
        new(dispatcher, await dispatcher.QueryAsync(new GetSchedulablePlansQuery()), date);

    public override string Title => "Thêm buổi vào lịch";
    public IReadOnlyList<SchedulablePlanDto> Plans { get; }
    public bool HasPlans => Plans.Count > 0;

    [ObservableProperty] public partial SchedulablePlanDto? SelectedPlan { get; set; }
    [ObservableProperty] public partial DateTime? Date { get; set; }
    [ObservableProperty] public partial TimeSpan? Time { get; set; }

    partial void OnSelectedPlanChanged(SchedulablePlanDto? value) => Time = value?.PreferredTime?.ToTimeSpan();

    [RelayCommand]
    private Task Submit()
    {
        if (SelectedPlan is null || Date is null)
        {
            ErrorMessage = SelectedPlan is null ? "Hãy chọn kế hoạch." : "Hãy chọn ngày.";
            return Task.CompletedTask;
        }
        return SubmitAsync(
            () => _dispatcher.SendAsync(new AddSessionCommand(SelectedPlan.PlanId, DateOnly.FromDateTime(Date.Value),
                                                              Time is { } t ? TimeOnly.FromTimeSpan(t) : null)),
            id => (Guid?)id);
    }
}
