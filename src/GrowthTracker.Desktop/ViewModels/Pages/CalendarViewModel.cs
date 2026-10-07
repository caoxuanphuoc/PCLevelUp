using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Calendar;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Calendar;
using GrowthTracker.Desktop.ViewModels.Dialogs;

namespace GrowthTracker.Desktop.ViewModels.Pages;

public enum CalendarMode
{
    Month,
    Week,
    List,
}

/// <summary>
/// Calendar (FR-20 → FR-25, UI Design §9, TDD §6.1): month grid, week timeline and list; filter by Area;
/// deadlines and Sunday reviews; peak days; drag a session to another day to reschedule; schedule metrics.
/// </summary>
public sealed partial class CalendarViewModel(IUseCaseDispatcher dispatcher, IDialogService dialogs, IClock clock)
    : PageViewModel, ISessionChipActions
{
    public override string Title => "Lịch biểu mục tiêu & kế hoạch";

    public ObservableCollection<AreaFilterItem> AreaFilters { get; } = [];
    public ObservableCollection<DayCellViewModel> Days { get; } = [];
    public ObservableCollection<WeekColumnViewModel> WeekColumns { get; } = [];
    public ObservableCollection<ListDayViewModel> ListDays { get; } = [];
    public ObservableCollection<SessionChipViewModel> SelectedSessions { get; } = [];
    public ObservableCollection<DeadlineChip> SelectedDeadlines { get; } = [];

    public static IReadOnlyList<string> WeekdayHeaders { get; } = ["T.Hai", "T.Ba", "T.Tư", "T.Năm", "T.Sáu", "T.Bảy", "Chủ Nhật"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonth), nameof(IsWeek), nameof(IsList), nameof(HeaderText))]
    public partial CalendarMode Mode { get; private set; } = CalendarMode.Month;

    public bool IsMonth => Mode == CalendarMode.Month;
    public bool IsWeek => Mode == CalendarMode.Week;
    public bool IsList => Mode == CalendarMode.List;

    /// <summary>The day the visible range is built around.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderText))]
    public partial DateOnly Anchor { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDateText), nameof(SelectedSummaryText), nameof(CanAddOnSelected))]
    public partial DateOnly SelectedDate { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeadlineCountText), nameof(DeadlineDetailText), nameof(SessionCountText), nameof(DoneProgress),
                              nameof(DoneText), nameof(AdherenceText), nameof(RescheduleText), nameof(BestDayText), nameof(BestDayDetailText),
                              nameof(PeakCountText))]
    public partial CalendarDto? Data { get; private set; }

    [ObservableProperty]
    public partial string? ActionError { get; private set; }

    private Guid? _areaId;

    public string HeaderText => Mode switch
    {
        CalendarMode.Week => $"Tuần {ISOWeek.GetWeekOfYear(Anchor.ToDateTime(TimeOnly.MinValue))} · " +
                             $"{WeekStart(Anchor):dd/MM} – {WeekStart(Anchor).AddDays(6):dd/MM/yyyy}",
        _ => $"Tháng {Anchor.Month}, {Anchor.Year}",
    };

    // ── Stats cards ──
    public string DeadlineCountText => Data is null ? "" : $"{Data.Stats.DeadlineCount}";
    public string DeadlineDetailText => Data is null ? "" : $"{Data.Stats.AtRiskDeadlines} At Risk";
    public string SessionCountText => Data is null ? "" : $"{Data.Stats.SessionCount}";
    public double DoneProgress => Data is { Stats.SessionCount: > 0 } d ? d.Stats.DoneCount * 100.0 / d.Stats.SessionCount : 0;
    public string DoneText => Data is null ? "" : $"{Data.Stats.DoneCount}/{Data.Stats.SessionCount} xong";
    public string AdherenceText => Data?.Stats.AdherencePct is { } a ? Display.Percent(a) : "—";
    public string RescheduleText => Data is null ? "" : $"Tỷ lệ dời lịch {Display.Number(Data.Stats.RescheduleRatePct)}%";
    public string BestDayText => Data?.Stats.BestWeekday is { } d ? WeekdayName(d) : "Chưa đủ dữ liệu";
    public string BestDayDetailText => Data?.Stats.BestWeekdayScore is { } s
        ? $"Score trung bình {Display.Number(s)}"
        : "Cần dữ liệu ít nhất 4 tuần";
    public string PeakCountText => Data is null ? "" : $"{Data.PeakDays.Count} ngày cao điểm";

    // ── Selected day panel ──
    public string SelectedDateText => Capitalize(SelectedDate.ToString("dddd, dd/MM/yyyy", Display.Culture));
    public string SelectedSummaryText => SelectedSessions.Count == 0 ? "Không có buổi nào"
        : $"{SelectedSessions.Count(s => s.IsDone)}/{SelectedSessions.Count(s => !s.IsSkipped)} buổi hoàn thành";
    public bool CanAddOnSelected => SelectedDate >= clock.Today;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        if (Anchor == default)
        {
            Anchor = clock.Today;
            SelectedDate = clock.Today;
        }
        return ReloadAsync();
    }

    public Task ReloadAsync() => LoadSafelyAsync(async () =>
    {
        var (from, to) = Range();
        var data = await dispatcher.QueryAsync(new GetCalendarQuery(from, to, _areaId));
        Build(data);
        Data = data;
    });

    // ── Navigation ──
    [RelayCommand]
    private Task SetMode(CalendarMode mode)
    {
        Mode = mode;
        Anchor = SelectedDate;
        return ReloadAsync();
    }

    [RelayCommand]
    private Task Previous() => Shift(-1);

    [RelayCommand]
    private Task Next() => Shift(1);

    /// <summary>"T" goes back to today (TDD §6.2).</summary>
    [RelayCommand]
    private Task GoToToday()
    {
        Anchor = clock.Today;
        SelectedDate = clock.Today;
        return ReloadAsync();
    }

    [RelayCommand]
    private Task SelectArea(AreaFilterItem item)
    {
        _areaId = item.AreaId;
        return ReloadAsync();
    }

    private Task Shift(int direction)
    {
        Anchor = Mode == CalendarMode.Week ? Anchor.AddDays(7 * direction) : Anchor.AddMonths(direction);
        return ReloadAsync();
    }

    // ── Actions ──
    [RelayCommand]
    private async Task AddSession()
    {
        var date = SelectedDate >= clock.Today ? SelectedDate : clock.Today;
        if (await dialogs.ShowAsync(await AddSessionDialogViewModel.CreateAsync(dispatcher, date)) is not null)
            await ReloadAsync();
    }

    public async Task RescheduleAsync(SessionChipViewModel session)
    {
        if (await dialogs.ShowAsync(new RescheduleDialogViewModel(dispatcher, session.Dto)) is not null)
            await ReloadAsync();
    }

    /// <summary>Drag & drop (TDD §6.3): dropping on a day asks "only this one / the following too", then reschedules.</summary>
    public async Task MoveSessionAsync(Guid occurrenceId, DateOnly target)
    {
        var session = Data?.Sessions.FirstOrDefault(s => s.OccurrenceId == occurrenceId);
        if (session is null || session.Date == target)
            return;
        if (target < clock.Today)
        {
            ActionError = ErrorMessages.For(new Domain.Common.Error("occurrence.past_date", ""));
            return;
        }
        ActionError = null;
        if (await dialogs.ShowAsync(new RescheduleDialogViewModel(dispatcher, session, target)) is not null)
        {
            SelectedDate = target;
            await ReloadAsync();
        }
    }

    public async Task OpenAsync(SessionChipViewModel session)
    {
        if (!session.CanOpen)
            return;
        var dialog = await PlanResultViewModel.OpenAsync(dispatcher, clock, session.OccurrenceId);
        if (dialog is not null && await dialogs.ShowAsync(dialog) is not null)
            await ReloadAsync();
    }

    // ── Building the views ──
    private (DateOnly From, DateOnly To) Range() => Mode switch
    {
        CalendarMode.Week => (WeekStart(Anchor), WeekStart(Anchor).AddDays(6)),
        // Six full weeks starting on the Monday on or before the 1st (MonthGrid: 7 × 6).
        CalendarMode.Month => (WeekStart(FirstOfMonth(Anchor)), WeekStart(FirstOfMonth(Anchor)).AddDays(41)),
        _ => (FirstOfMonth(Anchor), FirstOfMonth(Anchor).AddMonths(1).AddDays(-1)),
    };

    private void Build(CalendarDto data)
    {
        var today = data.Today;
        var chips = data.Sessions.Select(s => new SessionChipViewModel(s, today, this)).ToList();
        var byDate = chips.GroupBy(c => c.Dto.Date).ToDictionary(g => g.Key, g => (IReadOnlyList<SessionChipViewModel>)g.ToList());
        var deadlines = data.Deadlines.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => (IReadOnlyList<DeadlineChip>)g
            .Select(d => new DeadlineChip(d.Kind == DeadlineKind.Goal ? $"Hạn Goal: {d.Name}" : $"Milestone: {d.Name}",
                                          d.AreaColorHex, d.IsAtRisk, d.IsCompleted)).ToList());
        IReadOnlyList<SessionChipViewModel> SessionsOn(DateOnly d) => byDate.GetValueOrDefault(d) ?? [];
        IReadOnlyList<DeadlineChip> DeadlinesOn(DateOnly d) => deadlines.GetValueOrDefault(d) ?? [];

        AreaFilters.Clear();
        AreaFilters.Add(new AreaFilterItem(null, "Tất cả", "#0F172A", data.Areas.Sum(a => a.SessionCount)) { IsSelected = _areaId is null });
        foreach (var area in data.Areas)
            AreaFilters.Add(new AreaFilterItem(area.AreaId, area.Name, area.ColorHex, area.SessionCount) { IsSelected = _areaId == area.AreaId });

        Days.Clear();
        WeekColumns.Clear();
        ListDays.Clear();
        switch (Mode)
        {
            case CalendarMode.Month:
                for (var d = data.From; d <= data.To; d = d.AddDays(1))
                    Days.Add(new DayCellViewModel(d, d.Month == Anchor.Month, today, data.PeakDays.Contains(d),
                                                  SessionsOn(d), DeadlinesOn(d), SelectDayAsync) { IsSelected = d == SelectedDate });
                break;
            case CalendarMode.Week:
                for (var d = data.From; d <= data.To; d = d.AddDays(1))
                    WeekColumns.Add(new WeekColumnViewModel(d, today, data.PeakDays.Contains(d), SessionsOn(d), DeadlinesOn(d),
                                                            SelectColumnAsync) { IsSelected = d == SelectedDate });
                break;
            default:
                for (var d = data.From; d <= data.To; d = d.AddDays(1))
                    if (SessionsOn(d).Count > 0 || DeadlinesOn(d).Count > 0)
                        ListDays.Add(new ListDayViewModel(d, Capitalize(d.ToString("dddd, dd/MM", Display.Culture)), d == today,
                                                          data.PeakDays.Contains(d), SessionsOn(d), DeadlinesOn(d)));
                break;
        }

        ShowSelectedDay(SessionsOn(SelectedDate), DeadlinesOn(SelectedDate));
    }

    private Task SelectDayAsync(DayCellViewModel day)
    {
        foreach (var cell in Days)
            cell.IsSelected = ReferenceEquals(cell, day);
        SelectedDate = day.Date;
        ShowSelectedDay(day.Sessions, day.Deadlines);
        return Task.CompletedTask;
    }

    private Task SelectColumnAsync(WeekColumnViewModel column)
    {
        foreach (var c in WeekColumns)
            c.IsSelected = ReferenceEquals(c, column);
        SelectedDate = column.Date;
        ShowSelectedDay(column.AllDay.Concat(column.Blocks.Select(b => b.Session)).OrderBy(s => s.Dto.Time ?? TimeOnly.MaxValue).ToList(),
                        column.Deadlines);
        return Task.CompletedTask;
    }

    private void ShowSelectedDay(IReadOnlyList<SessionChipViewModel> sessions, IReadOnlyList<DeadlineChip> deadlines)
    {
        SelectedSessions.Clear();
        foreach (var s in sessions)
            SelectedSessions.Add(s);
        SelectedDeadlines.Clear();
        foreach (var d in deadlines)
            SelectedDeadlines.Add(d);
        OnPropertyChanged(nameof(SelectedSummaryText));
    }

    private static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);
    private static string WeekdayName(DayOfWeek d) => Capitalize(Display.Culture.DateTimeFormat.GetDayName(d));
    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Display.Culture) + text[1..];
}
