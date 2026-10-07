using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Logging;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Today;
using GrowthTracker.Domain.Plans;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels.Pages;

/// <summary>
/// Today (FR-30, UI Design §7): the day's sessions grouped by time of day, logging in one click for Binary plans
/// or through Plan Result, skipping with a reason, and the day's progress, streak, week rhythm and Area levels.
/// </summary>
public sealed partial class TodayViewModel(IUseCaseDispatcher dispatcher, IDialogService dialogs,
                                           INavigationService navigation, IClock clock) : PageViewModel, ISessionActions
{
    /// <summary>Quality used by the one-click tick on Binary plans; it can be changed within 24 hours.</summary>
    public const int QuickTickQuality = 4;

    public override string Title => "Hôm nay";

    public ObservableCollection<SessionGroupViewModel> Groups { get; } = [];
    public ObservableCollection<WeekDayItem> Week { get; } = [];
    public ObservableCollection<AreaLevelItem> Areas { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateText), nameof(WeekText), nameof(StreakText), nameof(LevelText), nameof(DayProgress),
                              nameof(DayProgressText), nameof(DayCountText), nameof(XpTodayText), nameof(HasSessions), nameof(IsEmpty),
                              nameof(MotivationText))]
    public partial TodayDto? Data { get; private set; }

    [ObservableProperty]
    public partial string? ActionError { get; private set; }

    public string DateText => Data is null ? "" : Capitalize(Data.Date.ToString("dddd, dd/MM/yyyy", Display.Culture));
    public string WeekText => Data is null ? "" : $"Tuần {Data.IsoWeek} / {Data.IsoYear}";
    public string StreakText => Data is null ? "" : $"{Data.Streak.Current} ngày streak";
    public string LevelText => Data is null ? "" : AreaLevelItem.Describe(Data.OverallLevel);
    public double DayProgress => Data is { PlannedCount: > 0 } d ? d.DoneCount * 100.0 / d.PlannedCount : 0;
    public string DayProgressText => $"{DayProgress:0}%";
    public string DayCountText => Data is null ? "" : $"({Data.DoneCount}/{Data.PlannedCount} hoàn thành)";
    public string XpTodayText => Data is null ? "" : $"+{Display.Number(Data.XpToday)} XP hôm nay";
    public bool HasSessions => Data?.Sessions.Count > 0;
    public bool IsEmpty => Data is not null && Data.Sessions.Count == 0;

    public string MotivationText => Data switch
    {
        null => "",
        { PlannedCount: 0 } => "Hôm nay không có buổi nào. Hãy nghỉ ngơi hoặc thêm kế hoạch mới.",
        var d when d.DoneCount >= d.PlannedCount => $"Đã xong mọi kế hoạch hôm nay. Chuỗi {d.Streak.Current} ngày đang được giữ vững!",
        var d when d.Streak.Current == 0 => $"Còn {d.PlannedCount - d.DoneCount} kế hoạch. Hoàn thành một buổi để bắt đầu chuỗi mới.",
        var d when d.DoneCount > 0 => $"Còn {d.PlannedCount - d.DoneCount} kế hoạch. Chuỗi {d.Streak.Current} ngày đã được giữ hôm nay.",
        var d => $"Còn {d.PlannedCount - d.DoneCount} kế hoạch. Hoàn thành một buổi để giữ chuỗi {d.Streak.Current} ngày.",
    };

    public override Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    public Task ReloadAsync() => LoadSafelyAsync(async () =>
    {
        var data = await dispatcher.QueryAsync(new GetTodayQuery());
        var now = clock.UtcNow;
        var items = data.Sessions.Select(s => new SessionItemViewModel(s, now, clock.TimeZone, this)).ToList();

        Groups.Clear();
        var logged = items.Where(i => i.IsLogged).ToList();
        if (logged.Count > 0)
            Groups.Add(new SessionGroupViewModel("ĐÃ GHI NHẬN", MaterialIconKind.CheckCircleOutline,
                $"Đã tích lũy +{Display.Number(data.XpToday)} XP", logged));
        foreach (var (title, icon, from, to) in Periods)
        {
            var pending = items.Where(i => i.IsPending && InPeriod(i.Dto.ScheduledTime, from, to)).ToList();
            if (pending.Count > 0)
                Groups.Add(new SessionGroupViewModel(title, icon, null, pending));
        }
        var skipped = items.Where(i => i.IsSkipped).ToList();
        if (skipped.Count > 0)
            Groups.Add(new SessionGroupViewModel("ĐÃ BỎ QUA", MaterialIconKind.Cancel, null, skipped, collapsible: true));

        Week.Clear();
        foreach (var day in data.Week)
            Week.Add(WeekItem(day, data.Date));
        Areas.Clear();
        foreach (var area in data.Areas.Where(a => a.Level.TotalXp > 0))
            Areas.Add(AreaLevelItem.From(area));

        Data = data;
    });

    [RelayCommand]
    private Task GoToGoals() => navigation.NavigateAsync<GoalsViewModel>();

    public async Task OpenAsync(SessionItemViewModel session)
    {
        var dialog = await PlanResultViewModel.OpenAsync(dispatcher, clock, session.OccurrenceId);
        if (dialog is null)
            return;
        if (await dialogs.ShowAsync(dialog) is not null)
            await ReloadAsync();
    }

    public async Task QuickDoneAsync(SessionItemViewModel session)
    {
        var result = await dispatcher.SendAsync(new LogPlanResultCommand(session.OccurrenceId,
            new LogEntry(1, Done: true, QuickTickQuality, null)));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        await ReloadAsync();
    }

    public async Task SkipAsync(SessionItemViewModel session, SkipReason reason)
    {
        var result = await dispatcher.SendAsync(new SkipOccurrenceCommand(session.OccurrenceId, reason));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        await ReloadAsync();
    }

    private static readonly (string Title, MaterialIconKind Icon, TimeOnly? From, TimeOnly? To)[] Periods =
    [
        ("SÁNG", MaterialIconKind.WeatherSunset, new TimeOnly(0, 0), new TimeOnly(12, 0)),
        ("CHIỀU", MaterialIconKind.WeatherSunny, new TimeOnly(12, 0), new TimeOnly(18, 0)),
        ("TỐI", MaterialIconKind.WeatherNight, new TimeOnly(18, 0), null),
        ("BẤT KỲ LÚC NÀO", MaterialIconKind.ClockOutline, null, null),
    ];

    /// <summary>Sessions without a preferred time go to "any time"; the others by [from, to).</summary>
    private static bool InPeriod(TimeOnly? time, TimeOnly? from, TimeOnly? to) =>
        from is null ? time is null
        : time is { } t && t >= from && (to is null || t < to);

    private static WeekDayItem WeekItem(WeekDayDto day, DateOnly today)
    {
        string label = day.Date.DayOfWeek == DayOfWeek.Sunday ? "CN" : $"T{(int)day.Date.DayOfWeek + 1}";
        if (day.AverageScore is { } score)
        {
            var hex = Display.ScoreTier(score).ColorHex;
            return new WeekDayItem(label, $"{Math.Round(score, MidpointRounding.AwayFromZero):0}", day.Date == today, true,
                                   Display.Brush(hex), Avalonia.Media.Brushes.White);
        }
        return new WeekDayItem(label, day.Planned > 0 && day.Date < today ? "0" : "–", day.Date == today, false,
                               Display.Tint("#64748B"), Display.Brush("#64748B"));
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Display.Culture) + text[1..];
}
