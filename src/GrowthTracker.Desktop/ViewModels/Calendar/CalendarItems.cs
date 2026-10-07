using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Calendar;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Domain.Plans;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels.Calendar;

/// <summary>What a session chip can ask the Calendar screen to do.</summary>
public interface ISessionChipActions
{
    Task RescheduleAsync(SessionChipViewModel session);
    Task OpenAsync(SessionChipViewModel session);
}

/// <summary>A session on the calendar: drag it to another day to reschedule (TDD §6.3), click it to open its result.</summary>
public sealed partial class SessionChipViewModel(CalendarSessionDto dto, DateOnly today, ISessionChipActions actions)
{
    public CalendarSessionDto Dto => dto;
    public Guid OccurrenceId => dto.OccurrenceId;
    public string Name => dto.PlanName;
    public string AreaName => dto.AreaName;
    public string ColorHex => dto.AreaColorHex;
    public string TimeText => dto.Time?.ToString("HH:mm") ?? "";
    public bool HasTime => dto.Time is not null;
    public string DurationText => $"{dto.DurationMinutes} phút";

    public bool IsDone => dto.State == OccurrenceState.Done;
    public bool IsMissed => dto.State == OccurrenceState.Missed;
    public bool IsSkipped => dto.State == OccurrenceState.Skipped;
    public bool IsScheduled => dto.State == OccurrenceState.Scheduled;
    public bool IsMoved => dto.IsMoved;

    /// <summary>Only sessions still waiting, from today on, can be moved (FR-24).</summary>
    public bool CanReschedule => IsScheduled && dto.Date >= today;
    /// <summary>Results exist only for today or earlier (FR-24).</summary>
    public bool CanOpen => dto.Date <= today && !IsSkipped;

    public MaterialIconKind StateIcon => dto.State switch
    {
        OccurrenceState.Done => MaterialIconKind.CheckCircle,
        OccurrenceState.Missed => MaterialIconKind.CloseCircleOutline,
        OccurrenceState.Skipped => MaterialIconKind.Cancel,
        _ => IsMoved ? MaterialIconKind.CalendarArrowRight : MaterialIconKind.CircleOutline,
    };

    public string StateText => dto.State switch
    {
        OccurrenceState.Done => dto.Score is { } s ? $"Xong · {s} điểm" : "Xong",
        OccurrenceState.Missed => "Bỏ lỡ",
        OccurrenceState.Skipped => "Đã bỏ qua",
        _ => IsMoved ? $"Dời từ {Display.Date(dto.OriginalDate)}" : "Chờ thực hiện",
    };

    public double Opacity => IsSkipped || IsMissed ? 0.55 : 1;

    [RelayCommand]
    private Task Reschedule() => actions.RescheduleAsync(this);

    [RelayCommand]
    private Task Open() => actions.OpenAsync(this);
}

public sealed record DeadlineChip(string Text, string ColorHex, bool IsAtRisk, bool IsCompleted)
{
    public MaterialIconKind Icon => IsCompleted ? MaterialIconKind.CheckCircleOutline : IsAtRisk ? MaterialIconKind.AlertOutline : MaterialIconKind.FlagOutline;
    public string DisplayColor => IsAtRisk ? "#DC2626" : ColorHex;
}

/// <summary>A day in the month grid (TDD §6.3 MonthGrid): up to 3 chips, then "+N khác".</summary>
public sealed partial class DayCellViewModel(DateOnly date, bool isCurrentMonth, DateOnly today, bool isPeak,
                                             IReadOnlyList<SessionChipViewModel> sessions, IReadOnlyList<DeadlineChip> deadlines,
                                             Func<DayCellViewModel, Task> select) : ObservableObject
{
    public const int VisibleChips = 3;

    public DateOnly Date => date;
    public string DayText => date.Day.ToString("00");
    public bool IsCurrentMonth => isCurrentMonth;
    public bool IsToday => date == today;
    public bool IsPast => date < today;
    public bool IsPeak => isPeak;
    /// <summary>Sundays carry the Weekly Review (FR-22).</summary>
    public bool IsReviewDay => date.DayOfWeek == DayOfWeek.Sunday;
    public bool AcceptsDrop => !IsPast;

    public IReadOnlyList<SessionChipViewModel> Sessions => sessions;
    public IReadOnlyList<SessionChipViewModel> VisibleSessions => sessions.Take(VisibleChips).ToList();
    public IReadOnlyList<DeadlineChip> Deadlines => deadlines;
    public bool HasDeadlines => deadlines.Count > 0;
    public string MoreText => sessions.Count > VisibleChips ? $"+{sessions.Count - VisibleChips} khác" : "";
    public bool HasMore => sessions.Count > VisibleChips;

    public string SummaryText => sessions.Count == 0 ? ""
        : IsPast || IsToday ? $"{sessions.Count(s => s.IsDone)}/{sessions.Count(s => !s.IsSkipped)} xong"
        : $"{sessions.Count} buổi";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private Task Select() => select(this);
}

/// <summary>A positioned block in the week timeline (TDD §6.3 WeekTimeline).</summary>
public sealed record TimedBlock(SessionChipViewModel Session, double Top, double Height)
{
    public string Name => Session.Name;
    public string TimeText => Session.TimeText;
    public string ColorHex => Session.ColorHex;
    public string StateText => Session.StateText;
    public double Opacity => Session.Opacity;
}

/// <summary>One day column of the week view: sessions without a time on top, timed blocks on an hour canvas.</summary>
public sealed partial class WeekColumnViewModel(DateOnly date, DateOnly today, bool isPeak, IReadOnlyList<SessionChipViewModel> sessions,
                                                IReadOnlyList<DeadlineChip> deadlines, Func<WeekColumnViewModel, Task> select) : ObservableObject
{
    public const int FirstHour = 6;
    public const int LastHour = 24;
    public const double PixelsPerHour = 48;
    public static double CanvasHeight => (LastHour - FirstHour) * PixelsPerHour;
    public static IReadOnlyList<string> HourLabels { get; } =
        Enumerable.Range(FirstHour, LastHour - FirstHour).Select(h => $"{h:00}:00").ToList();

    public DateOnly Date => date;
    public string DayName => date.DayOfWeek == DayOfWeek.Sunday ? "CN" : $"T{(int)date.DayOfWeek + 1}";
    public string DayText => date.ToString("dd/MM");
    public bool IsToday => date == today;
    public bool IsPeak => isPeak;
    public bool AcceptsDrop => date >= today;
    public IReadOnlyList<DeadlineChip> Deadlines => deadlines;
    public IReadOnlyList<SessionChipViewModel> AllDay => sessions.Where(s => !s.HasTime).ToList();

    public IReadOnlyList<TimedBlock> Blocks => sessions.Where(s => s.HasTime).Select(s =>
    {
        double minutes = s.Dto.Time!.Value.ToTimeSpan().TotalMinutes - FirstHour * 60;
        double top = Math.Clamp(minutes, 0, (LastHour - FirstHour) * 60 - 20) / 60 * PixelsPerHour;
        double height = Math.Max(22, s.Dto.DurationMinutes / 60.0 * PixelsPerHour);
        return new TimedBlock(s, top, Math.Min(height, CanvasHeight - top));
    }).ToList();

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private Task Select() => select(this);
}

/// <summary>A day in the list view, with its sessions and deadlines.</summary>
public sealed record ListDayViewModel(DateOnly Date, string Title, bool IsToday, bool IsPeak,
                                      IReadOnlyList<SessionChipViewModel> Sessions, IReadOnlyList<DeadlineChip> Deadlines);
