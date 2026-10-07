using System.Globalization;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Settings;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Gamification;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Progress;
using GrowthTracker.Domain.Risk;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Dashboard;

public enum DashboardPeriod
{
    Week,
    Month,
    Quarter,
}

// ── GetDashboardSummary ─────────────────────────────────────────────────────

/// <summary>Everything on the Dashboard except the trend chart (UI Design §5, TDD §3.1).</summary>
public sealed record GetDashboardSummaryQuery(DashboardPeriod Period = DashboardPeriod.Week) : IQuery<DashboardDto>;

/// <param name="OverallScore">Average Score of the logs in the period (BRD §7 Overall Score); null when nothing was logged.</param>
/// <param name="DaysOfData">Days since the first logged session; trends show from 7 days (BR-13).</param>
/// <param name="ShowRiskBanner">At Risk Goals exist and the banner was not snoozed.</param>
public sealed record DashboardDto(
    DateOnly Today,
    DashboardPeriod Period,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal? OverallScore,
    decimal? PreviousScore,
    Trend? ScoreTrend,
    LevelInfo OverallLevel,
    Streak Streak,
    int DaysOfData,
    bool HasEnoughData,
    int GoalCount,
    int PlanCount,
    IReadOnlyList<AreaCardDto> Areas,
    IReadOnlyList<RiskGoalDto> AtRiskGoals,
    bool ShowRiskBanner)
{
    /// <summary>No Goal yet: the Dashboard shows the three getting-started steps instead of numbers.</summary>
    public bool IsNewUser => GoalCount == 0;
}

/// <param name="Progress">Average progress of the Area's open Goals, weighted by priority (BRD §5.5).</param>
/// <param name="ExpectedProgress">The same average of where those Goals should be by now (the bar's marker).</param>
public sealed record AreaCardDto(
    Guid AreaId,
    string Name,
    string ColorHex,
    decimal? Score,
    decimal? PreviousScore,
    Trend? Trend,
    decimal Progress,
    decimal ExpectedProgress,
    int GoalCount,
    bool HasAtRisk,
    LevelInfo Level);

public sealed record RiskGoalDto(Guid GoalId, string Name, string AreaName, decimal Progress, decimal ExpectedProgress);

internal sealed class GetDashboardSummaryHandler(IAppDbContext db, IClock clock, LevelTable levels, DomainThresholds thresholds)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardDto>
{
    public const int MaxAreaCards = 6;

    public async Task<DashboardDto> HandleAsync(GetDashboardSummaryQuery q, CancellationToken ct)
    {
        var today = clock.Today;
        var (start, end) = DashboardPeriods.Range(q.Period, today);
        var previousStart = DashboardPeriods.PreviousStart(q.Period, start);

        var areas = await db.Areas.IgnoreQueryFilters().ToDictionaryAsync(a => a.Id, ct);
        var goals = await db.Goals.Where(g => g.Status != GoalStatus.Cancelled).ToListAsync(ct);
        var openGoals = goals.Where(g => !g.IsClosed).ToList();
        var links = await db.PlanGoalLinks.ToListAsync(ct);
        var goalArea = goals.ToDictionary(g => g.Id, g => g.AreaId);

        // Every log with the day it belongs to and the Areas its plan feeds (by link weight, like XP).
        var logs = await (from l in db.PlanLogs
                          join o in db.PlanOccurrences on l.OccurrenceId equals o.Id
                          select new { o.PlanId, o.ScheduledDate, l.Score }).ToListAsync(ct);
        var planAreas = links.Where(l => goalArea.ContainsKey(l.GoalId))
                             .GroupBy(l => l.PlanId)
                             .ToDictionary(g => g.Key, g => g.Select(l => goalArea[l.GoalId]).Distinct().ToList());

        decimal? Average(IEnumerable<int> scores)
        {
            var list = scores.ToList();
            return list.Count == 0 ? null : Math.Round((decimal)list.Average(), 1);
        }
        decimal? ScoreIn(DateOnly from, DateOnly to, Guid? areaId = null) => Average(
            logs.Where(l => l.ScheduledDate >= from && l.ScheduledDate <= to
                            && (areaId is null || (planAreas.GetValueOrDefault(l.PlanId)?.Contains(areaId.Value) ?? false)))
                .Select(l => l.Score));

        var current = ScoreIn(start, today);
        var previous = ScoreIn(previousStart, start.AddDays(-1));
        Trend? Compare(decimal? now, decimal? before) =>
            now is { } n && before is { } b ? TrendClassifier.Compare(n, b, thresholds.TrendThresholdPoints) : null;

        var xpPerArea = await XpTotals.PerAreaAsync(db, ct);
        var cards = areas.Values
            .Where(a => !a.IsArchived && (openGoals.Any(g => g.AreaId == a.Id) || xpPerArea.ContainsKey(a.Id)))
            .OrderBy(a => a.SortOrder)
            .Take(MaxAreaCards)
            .Select(a =>
            {
                var areaGoals = openGoals.Where(g => g.AreaId == a.Id).ToList();
                var now = ScoreIn(start, today, a.Id);
                var before = ScoreIn(previousStart, start.AddDays(-1), a.Id);
                return new AreaCardDto(
                    a.Id, a.Name, a.ColorHex, now, before, Compare(now, before),
                    Math.Round(ProgressCalculator.WeightedAverage(areaGoals.Select(g => (g.Progress, ProgressCalculator.PriorityWeight(g.Priority)))), 1),
                    Math.Round(ProgressCalculator.WeightedAverage(areaGoals.Select(g => (ProgressCalculator.Expected(g.StartDate, g.TargetDate, today), ProgressCalculator.PriorityWeight(g.Priority)))), 1),
                    areaGoals.Count, areaGoals.Any(g => g.Status == GoalStatus.AtRisk),
                    levels.For(xpPerArea.GetValueOrDefault(a.Id)));
            })
            .ToList();

        var atRisk = openGoals.Where(g => g.Status == GoalStatus.AtRisk)
            .OrderBy(g => g.Progress - g.ExpectedProgress)
            .Select(g => new RiskGoalDto(g.Id, g.Name, areas.TryGetValue(g.AreaId, out var a) ? a.Name : "",
                                         g.Progress, ProgressCalculator.Expected(g.StartDate, g.TargetDate, today)))
            .ToList();
        var snoozedUntil = await DashboardPeriods.RiskBannerSnoozedUntilAsync(db, ct);

        int daysOfData = logs.Count == 0 ? 0 : today.DayNumber - logs.Min(l => l.ScheduledDate).DayNumber + 1;
        var doneDays = await XpTotals.DoneDaysAsync(db, today, ct);

        return new DashboardDto(
            today, q.Period, start, end, current, previous, Compare(current, previous),
            levels.For(xpPerArea.Values.Sum()),
            StreakCalculator.Calculate(doneDays, [], today),
            daysOfData, daysOfData >= thresholds.MinDaysForTrends,
            goals.Count, await db.Plans.CountAsync(p => p.Status == PlanStatus.Active, ct),
            cards, atRisk,
            ShowRiskBanner: atRisk.Count > 0 && (snoozedUntil is null || today > snoozedUntil));
    }
}

// ── GetScoreTrend ───────────────────────────────────────────────────────────

/// <summary>Weekly average Score for the last N weeks, ending with the current week (UI Design §5.2 trend chart).</summary>
public sealed record GetScoreTrendQuery(int Weeks = 6) : IQuery<ScoreTrendDto>;

public sealed record ScoreTrendDto(IReadOnlyList<WeekPointDto> Points, Trend? Trend, bool HasEnoughData);

/// <param name="Done">Sessions done that week.</param>
/// <param name="Planned">Sessions that week that were not skipped.</param>
public sealed record WeekPointDto(DateOnly WeekStart, int IsoWeek, decimal? AverageScore, int Done, int Planned);

internal sealed class GetScoreTrendHandler(IAppDbContext db, IClock clock, DomainThresholds thresholds)
    : IQueryHandler<GetScoreTrendQuery, ScoreTrendDto>
{
    public async Task<ScoreTrendDto> HandleAsync(GetScoreTrendQuery q, CancellationToken ct)
    {
        var today = clock.Today;
        var thisWeek = DashboardPeriods.WeekStart(today);
        var from = thisWeek.AddDays(-7 * (q.Weeks - 1));
        var to = thisWeek.AddDays(6);

        var sessions = await db.PlanOccurrences.Where(o => o.ScheduledDate >= from && o.ScheduledDate <= to)
                                               .Select(o => new { o.Id, o.ScheduledDate, o.State }).ToListAsync(ct);
        var ids = sessions.Select(s => s.Id).ToList();
        var scores = await db.PlanLogs.Where(l => ids.Contains(l.OccurrenceId)).ToDictionaryAsync(l => l.OccurrenceId, l => l.Score, ct);

        var points = Enumerable.Range(0, q.Weeks).Select(i =>
        {
            var start = from.AddDays(7 * i);
            var week = sessions.Where(s => s.ScheduledDate >= start && s.ScheduledDate <= start.AddDays(6)).ToList();
            var weekScores = week.Where(s => scores.ContainsKey(s.Id)).Select(s => scores[s.Id]).ToList();
            return new WeekPointDto(start, ISOWeek.GetWeekOfYear(start.ToDateTime(TimeOnly.MinValue)),
                                    weekScores.Count == 0 ? null : Math.Round((decimal)weekScores.Average(), 1),
                                    week.Count(s => s.State == OccurrenceState.Done),
                                    week.Count(s => s.State != OccurrenceState.Skipped));
        }).ToList();

        var firstLog = await (from l in db.PlanLogs
                              join o in db.PlanOccurrences on l.OccurrenceId equals o.Id
                              select (DateOnly?)o.ScheduledDate).MinAsync(ct);
        bool enough = firstLog is { } first && today.DayNumber - first.DayNumber + 1 >= thresholds.MinDaysForTrends;
        return new ScoreTrendDto(points, enough ? TrendClassifier.Classify(points.Select(p => p.AverageScore), thresholds.TrendThresholdPoints) : null, enough);
    }
}

// ── SnoozeRiskBanner ────────────────────────────────────────────────────────

/// <summary>"Bỏ qua 7 ngày" on the Dashboard risk banner (UI Design §5.2). Returns the last snoozed day.</summary>
public sealed record SnoozeRiskBannerCommand(int Days = 7) : ICommand<DateOnly>;

internal sealed class SnoozeRiskBannerHandler(IAppDbContext db, IClock clock) : ICommandHandler<SnoozeRiskBannerCommand, DateOnly>
{
    public async Task<Result<DateOnly>> HandleAsync(SnoozeRiskBannerCommand command, CancellationToken ct)
    {
        var until = clock.Today.AddDays(command.Days - 1);
        await SettingKeys.SetDateAsync(db, DashboardPeriods.RiskBannerSnoozeKey, until, ct);
        await db.SaveChangesAsync(ct);
        return Result<DateOnly>.Success(until);
    }
}

internal static class DashboardPeriods
{
    public const string RiskBannerSnoozeKey = "Dashboard.RiskBannerSnoozedUntil";

    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>The calendar week (Mon–Sun), month or quarter containing <paramref name="today"/>.</summary>
    public static (DateOnly Start, DateOnly End) Range(DashboardPeriod period, DateOnly today) => period switch
    {
        DashboardPeriod.Week => (WeekStart(today), WeekStart(today).AddDays(6)),
        DashboardPeriod.Month => (new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)),
        _ => (new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1), new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1).AddMonths(3).AddDays(-1)),
    };

    public static DateOnly PreviousStart(DashboardPeriod period, DateOnly start) => period switch
    {
        DashboardPeriod.Week => start.AddDays(-7),
        DashboardPeriod.Month => start.AddMonths(-1),
        _ => start.AddMonths(-3),
    };

    public static async Task<DateOnly?> RiskBannerSnoozedUntilAsync(IAppDbContext db, CancellationToken ct)
    {
        var value = await db.AppSettings.Where(s => s.Key == RiskBannerSnoozeKey).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}
