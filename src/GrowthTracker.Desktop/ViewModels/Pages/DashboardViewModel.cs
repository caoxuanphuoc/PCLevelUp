using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Dashboard;
using GrowthTracker.Application.Logging;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Today;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Progress;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Material.Icons;
using SkiaSharp;

namespace GrowthTracker.Desktop.ViewModels.Pages;

public sealed record PeriodOption(DashboardPeriod Value, string Label);

/// <summary>An Area card (UI Design §5.2): Score with ↑ → ↓ against the previous period, progress and Level.</summary>
public sealed partial class AreaCardViewModel(AreaCardDto dto, Func<AreaCardViewModel, Task> open)
{
    public AreaCardDto Dto => dto;
    public string Name => dto.Name;
    public string ColorHex => dto.ColorHex;
    public IBrush ColorBrush => Display.Brush(dto.ColorHex);
    public string ScoreText => dto.Score is { } s ? Math.Round(s, MidpointRounding.AwayFromZero).ToString("0") : "—";
    public IBrush ScoreBrush => Display.Brush(dto.Score is { } s ? Display.ScoreTier(s).ColorHex : "#94A3B8");
    public MaterialIconKind TrendIcon => Trends.Icon(dto.Trend);
    public IBrush TrendBrush => Trends.Brush(dto.Trend);
    public string TrendText => dto.Score is { } s && dto.PreviousScore is { } p ? $"{(s - p >= 0 ? "+" : "")}{Display.Number(Math.Round(s - p, 1))}" : "";
    public double Progress => (double)dto.Progress;
    public double Expected => (double)dto.ExpectedProgress;
    public string ExpectedText => $"Kỳ vọng: {Display.Percent(dto.ExpectedProgress)}";
    public string ProgressText => Display.Percent(dto.Progress);
    public string LevelText => $"Lv {dto.Level.Level}";
    public string GoalText => dto.GoalCount == 0 ? "Chưa có mục tiêu" : $"{dto.GoalCount} mục tiêu";
    public bool HasAtRisk => dto.HasAtRisk;

    [RelayCommand]
    private Task Open() => open(this);
}

internal static class Trends
{
    public static MaterialIconKind Icon(Trend? trend) => trend switch
    {
        Trend.Improving => MaterialIconKind.ArrowUp,
        Trend.Declining => MaterialIconKind.ArrowDown,
        _ => MaterialIconKind.ArrowRight,
    };

    public static IBrush Brush(Trend? trend) => Display.Brush(trend switch
    {
        Trend.Improving => "#16A34A",
        Trend.Declining => "#DC2626",
        _ => "#64748B",
    });

    public static string Label(Trend? trend) => trend switch
    {
        Trend.Improving => "Đang cải thiện",
        Trend.Declining => "Đang giảm",
        Trend.Stable => "Ổn định",
        _ => "",
    };
}

/// <summary>
/// Dashboard (UI Design §5, TDD §6.1): overall Score / Level / XP / streak, the 6-week Score trend (LiveCharts2),
/// Area cards, a short Today block and the At Risk banner. The period filter (week / month / quarter) drives
/// every figure except the trend chart, which is always the last 6 weeks.
/// </summary>
public sealed partial class DashboardViewModel(IUseCaseDispatcher dispatcher, IDialogService dialogs, INavigationService navigation,
                                               IClock clock) : PageViewModel, ISessionActions
{
    public const int TodayItems = 4;

    public override string Title => "Tổng quan";

    public static IReadOnlyList<PeriodOption> Periods { get; } =
    [
        new(DashboardPeriod.Week, "Tuần"),
        new(DashboardPeriod.Month, "Tháng"),
        new(DashboardPeriod.Quarter, "Quý"),
    ];

    public ObservableCollection<AreaCardViewModel> Areas { get; } = [];
    public ObservableCollection<SessionItemViewModel> TodaySessions { get; } = [];
    public ObservableCollection<AreaLevelItem> AreaXp { get; } = [];

    [ObservableProperty]
    public partial PeriodOption SelectedPeriod { get; set; } = Periods[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Greeting), nameof(IsNewUser), nameof(HasData), nameof(ScoreValue), nameof(ScoreText),
                              nameof(ScoreLabel), nameof(ScoreBrush), nameof(ScoreTrendIcon), nameof(ScoreTrendBrush), nameof(ScoreTrendText),
                              nameof(LevelText), nameof(XpText), nameof(StreakText), nameof(LongestStreakText), nameof(ShowRiskBanner),
                              nameof(RiskText), nameof(CollectingData), nameof(StepGoalDone), nameof(StepPlanDone),
                              nameof(HasReviewReminder), nameof(ReviewReminderText))]
    public partial DashboardDto? Data { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendLabel), nameof(TrendBrush), nameof(ShowTrendChart))]
    public partial ScoreTrendDto? TrendData { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TodayTitle), nameof(TodaySubtitle), nameof(MoreTodayText), nameof(HasMoreToday), nameof(HasToday))]
    public partial TodayDto? TodayData { get; private set; }

    [ObservableProperty]
    public partial bool IsOverallExpanded { get; set; }

    [ObservableProperty]
    public partial string? ActionError { get; private set; }

    // ── Chart (LiveCharts2) ──
    [ObservableProperty] public partial ISeries[] TrendSeries { get; private set; } = [];
    [ObservableProperty] public partial Axis[] TrendXAxes { get; private set; } = [new Axis()];
    [ObservableProperty] public partial Axis[] TrendYAxes { get; private set; } = [new Axis()];

    // ── Header & overall ──
    public string Greeting
    {
        get
        {
            var hour = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), clock.TimeZone).Hour;
            return hour < 12 ? "Chào buổi sáng" : hour < 18 ? "Chào buổi chiều" : "Chào buổi tối";
        }
    }

    public bool IsNewUser => Data?.IsNewUser == true;
    public bool HasData => Data is { IsNewUser: false };
    public bool CollectingData => Data is { IsNewUser: false, HasEnoughData: false };

    public double ScoreValue => (double)(Data?.OverallScore ?? 0);
    public string ScoreText => Data?.OverallScore is { } s ? Math.Round(s, MidpointRounding.AwayFromZero).ToString("0") : "—";
    public string ScoreLabel => Data?.OverallScore is { } s ? Display.ScoreTier(s).Label : "Chưa có Score trong kỳ";
    public IBrush ScoreBrush => Display.Brush(Data?.OverallScore is { } s ? Display.ScoreTier(s).ColorHex : "#94A3B8");
    public MaterialIconKind ScoreTrendIcon => Trends.Icon(Data?.ScoreTrend);
    public IBrush ScoreTrendBrush => Trends.Brush(Data?.ScoreTrend);
    public string ScoreTrendText => Data is { OverallScore: { } s, PreviousScore: { } p }
        ? $"{(s - p >= 0 ? "+" : "")}{Display.Number(Math.Round(s - p, 1))} so với kỳ trước"
        : "Chưa có kỳ trước để so sánh";
    public string LevelText => Data is null ? "" : $"Level {Data.OverallLevel.Level}";
    public string XpText => Data is null ? "" : $"{Display.Number(Data.OverallLevel.TotalXp)} XP";
    public string StreakText => Data is null ? "" : $"Streak {Data.Streak.Current} ngày";
    public string LongestStreakText => Data is null ? "" : $"Chuỗi dài nhất: {Data.Streak.Longest} ngày";

    // ── New user steps (UI Design §5.4) ──
    public bool StepGoalDone => Data?.GoalCount > 0;
    public bool StepPlanDone => Data?.PlanCount > 0;

    // ── Trend ──
    public bool ShowTrendChart => TrendData?.HasEnoughData == true;
    public string TrendLabel => TrendData?.HasEnoughData == true ? Trends.Label(TrendData.Trend) : "Đang thu thập dữ liệu";
    public IBrush TrendBrush => Trends.Brush(TrendData?.Trend);

    // ── Risk banner ──
    public bool ShowRiskBanner => Data?.ShowRiskBanner == true;
    public string RiskText => Data?.AtRiskGoals is { Count: > 0 } goals
        ? goals.Count == 1
            ? $"\"{goals[0].Name}\" đang At Risk: tiến độ {Display.Percent(goals[0].Progress)}, kỳ vọng {Display.Percent(goals[0].ExpectedProgress)}."
            : $"{goals.Count} mục tiêu đang At Risk, chậm nhất là \"{goals[0].Name}\" ({Display.Percent(goals[0].Progress)} / kỳ vọng {Display.Percent(goals[0].ExpectedProgress)})."
        : "";

    // ── Weekly Review reminder (Sunday evening to Wednesday, until completed) ──
    public bool HasReviewReminder => Data?.ReviewReminderWeek is not null;
    public string ReviewReminderText => Data?.ReviewReminderWeek is { } week
        ? $"Weekly Review tuần {System.Globalization.ISOWeek.GetWeekOfYear(week.ToDateTime(TimeOnly.MinValue))} đã sẵn sàng. Dành 2–3 phút nhìn lại tuần và điều chỉnh kế hoạch."
        : "";

    [RelayCommand]
    private Task OpenReview() => Data?.ReviewReminderWeek is { } week
        ? navigation.NavigateAsync<WeeklyReviewViewModel>(week)
        : navigation.NavigateAsync<WeeklyReviewViewModel>();

    // ── Today block ──
    public bool HasToday => TodayData?.Sessions.Count > 0;
    public string TodayTitle => TodayData is null ? "HÔM NAY" : $"HÔM NAY ({TodayData.DoneCount}/{TodayData.PlannedCount})";
    public string TodaySubtitle => TodayData is null || TodayData.PlannedCount == 0
        ? "Hôm nay không có kế hoạch nào trên lịch."
        : TodayData.DoneCount >= TodayData.PlannedCount
            ? $"Bạn đã hoàn thành cả {TodayData.PlannedCount} kế hoạch hôm nay."
            : $"Hôm nay bạn có {TodayData.PlannedCount} kế hoạch, đã hoàn thành {TodayData.DoneCount}.";
    public bool HasMoreToday => TodayData?.Sessions.Count > TodayItems;
    public string MoreTodayText => HasMoreToday ? $"+ {TodayData!.Sessions.Count - TodayItems} Plan khác" : "";

    public override Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    partial void OnSelectedPeriodChanged(PeriodOption value) => _ = ReloadAsync();

    public Task ReloadAsync() => LoadSafelyAsync(async () =>
    {
        var summary = await dispatcher.QueryAsync(new GetDashboardSummaryQuery(SelectedPeriod.Value));
        var trend = await dispatcher.QueryAsync(new GetScoreTrendQuery());
        var today = await dispatcher.QueryAsync(new GetTodayQuery());

        Areas.Clear();
        foreach (var area in summary.Areas)
            Areas.Add(new AreaCardViewModel(area, card => navigation.NavigateAsync<GoalsViewModel>(card.Dto.AreaId)));
        AreaXp.Clear();
        foreach (var area in summary.Areas.Where(a => a.Level.TotalXp > 0))
            AreaXp.Add(AreaLevelItem.From(new AreaLevelDto(area.AreaId, area.Name, area.ColorHex, area.Level)));

        var now = clock.UtcNow;
        TodaySessions.Clear();
        foreach (var session in today.Sessions.Where(s => s.State != OccurrenceState.Skipped).Take(TodayItems))
            TodaySessions.Add(new SessionItemViewModel(session, now, clock.TimeZone, this));

        BuildChart(trend);
        Data = summary;
        TrendData = trend;
        TodayData = today;
    });

    private void BuildChart(ScoreTrendDto trend)
    {
        var blue = new SKColor(0x25, 0x63, 0xEB);
        var grey = new SKColor(0x94, 0xA3, 0xB8);
        var points = trend.Points;
        TrendSeries =
        [
            new LineSeries<ObservableValue>
            {
                Name = "Score",
                Values = points.Select(p => new ObservableValue(p.AverageScore is { } s ? (double)s : null)).ToList(),
                Stroke = new SolidColorPaint(blue, 3),
                Fill = new SolidColorPaint(blue.WithAlpha(0x30)),
                GeometrySize = 10,
                GeometryStroke = new SolidColorPaint(blue, 3),
                GeometryFill = new SolidColorPaint(SKColors.White),
                LineSmoothness = 0.6,
                YToolTipLabelFormatter = point =>
                {
                    var week = points[point.Index];
                    return $"{point.Coordinate.PrimaryValue:0} điểm · {week.Done}/{week.Planned} buổi";
                },
            },
        ];
        TrendXAxes =
        [
            new Axis
            {
                Labels = points.Select(p => $"T{p.IsoWeek}").ToArray(),
                LabelsPaint = new SolidColorPaint(grey),
                TextSize = 12,
            },
        ];
        TrendYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                MinStep = 20,
                LabelsPaint = new SolidColorPaint(grey),
                SeparatorsPaint = new SolidColorPaint(grey.WithAlpha(0x40), 1),
                TextSize = 12,
            },
        ];
    }

    // ── Commands ──
    [RelayCommand]
    private Task LogNow() => navigation.NavigateAsync<TodayViewModel>();

    [RelayCommand]
    private void ToggleOverall() => IsOverallExpanded = !IsOverallExpanded;

    [RelayCommand]
    private Task OpenRiskGoal() =>
        Data?.AtRiskGoals is { Count: > 0 } goals ? navigation.NavigateAsync<GoalDetailViewModel>(goals[0].GoalId) : Task.CompletedTask;

    [RelayCommand]
    private async Task SnoozeRisk()
    {
        await dispatcher.SendAsync(new SnoozeRiskBannerCommand());
        await ReloadAsync();
    }

    [RelayCommand]
    private Task GoToGoals() => navigation.NavigateAsync<GoalsViewModel>();

    [RelayCommand]
    private Task GoToSettings() => navigation.NavigateAsync<SettingsViewModel>();

    // ── Today block actions (same behaviour as the Today screen) ──
    public async Task OpenAsync(SessionItemViewModel session)
    {
        var dialog = await PlanResultViewModel.OpenAsync(dispatcher, clock, session.OccurrenceId);
        if (dialog is not null && await dialogs.ShowAsync(dialog) is not null)
            await ReloadAsync();
    }

    public async Task QuickDoneAsync(SessionItemViewModel session)
    {
        var result = await dispatcher.SendAsync(new LogPlanResultCommand(session.OccurrenceId,
            new LogEntry(1, Done: true, TodayViewModel.QuickTickQuality, null)));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        await ReloadAsync();
    }

    public async Task SkipAsync(SessionItemViewModel session, SkipReason reason)
    {
        var result = await dispatcher.SendAsync(new SkipOccurrenceCommand(session.OccurrenceId, reason));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        await ReloadAsync();
    }
}
