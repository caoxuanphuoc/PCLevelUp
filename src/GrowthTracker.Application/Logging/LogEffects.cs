using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Gamification;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.PlanLogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Progress;
using GrowthTracker.Domain.Risk;
using GrowthTracker.Domain.Scheduling;
using GrowthTracker.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Logging;

/// <summary>What the user entered for one session.</summary>
/// <param name="Actual">Minutes / units for Time and Quantity plans; ignored otherwise.</param>
/// <param name="Done">For Binary and Frequency plans: whether the session was done.</param>
/// <param name="CriteriaDoneIds">For Achievement plans: the criteria that are done after this session.</param>
public sealed record LogEntry(decimal Actual, bool Done, int Quality, string? Note, IReadOnlyCollection<Guid>? CriteriaDoneIds = null);

internal static class ScoreInputs
{
    /// <summary>
    /// Builds the Score Engine input for a session. Consistency is the rolling 7-day window ending on the
    /// session's date: done sessions ÷ scheduled sessions, where skipped and missed ones still count (BR-10).
    /// </summary>
    public static async Task<ScoreInput> BuildAsync(IAppDbContext db, Plan plan, PlanOccurrence occurrence,
                                                    LogEntry entry, CancellationToken ct)
    {
        var from = occurrence.ScheduledDate.AddDays(-6);
        var window = await db.PlanOccurrences
            .Where(o => o.PlanId == plan.Id && o.ScheduledDate >= from && o.ScheduledDate <= occurrence.ScheduledDate)
            .Select(o => new { o.Id, o.State })
            .ToListAsync(ct);

        bool countsAsDone = CountsAsDone(plan.PlanType, entry, plan);
        int done = window.Count(o => o.Id != occurrence.Id && o.State == OccurrenceState.Done) + (countsAsDone ? 1 : 0);
        int target = Math.Max(1, window.Count + (window.Any(o => o.Id == occurrence.Id) ? 0 : 1));

        return new ScoreInput(
            plan.PlanType, plan.Difficulty, entry.Quality,
            SessionsDoneLast7Days: done,
            SessionsTargetLast7Days: target,
            Actual: entry.Actual,
            Target: plan.TargetValue,
            Done: entry.Done,
            CriteriaDone: plan.Criteria.Count(c => c.IsDone),
            CriteriaTotal: plan.Criteria.Count);
    }

    /// <summary>A session counts toward Consistency when something was actually done.</summary>
    public static bool CountsAsDone(PlanType type, LogEntry entry, Plan plan) => type switch
    {
        PlanType.Time or PlanType.Quantity => entry.Actual > 0,
        PlanType.Achievement => plan.Criteria.Any(c => c.IsDone),
        _ => entry.Done,
    };
}

/// <summary>
/// Everything a Plan Log changes besides the log itself, applied in the same transaction (TDD §2.2):
/// XP into the append-only ledger per Area (BR-07), and Plan → Milestone → Goal progress, Score and risk status.
/// </summary>
internal sealed class LogEffects(IAppDbContext db, RiskDetector risk, DomainThresholds thresholds)
{
    /// <summary>Credits XP (or an edit's difference) to the Areas of the Goals the plan contributes to.</summary>
    public async Task CreditXpAsync(Plan plan, Guid planLogId, decimal xp, string reason, CancellationToken ct)
    {
        if (xp == 0)
            return;
        var goalIds = plan.GoalLinks.Select(l => l.GoalId).ToList();
        var areaOfGoal = await db.Goals.IgnoreQueryFilters()
            .Where(g => goalIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.AreaId, ct);

        var shares = XpCalculator.Split(xp, plan.GoalLinks.Select(l => (l.GoalId, l.Weight)).ToList());
        foreach (var byArea in shares.GroupBy(s => areaOfGoal[s.Key]))
            db.XpLedger.Add(new XpLedgerEntry(planLogId, byArea.Key, byArea.Sum(s => s.Amount), reason));
    }

    /// <summary>Recomputes cached progress, score and status for every Goal the plan contributes to.</summary>
    public async Task RecomputeGoalsAsync(Plan plan, DateOnly today, CancellationToken ct)
    {
        foreach (var goalId in plan.GoalLinks.Select(l => l.GoalId).Distinct())
            await RecomputeGoalAsync(goalId, today, ct);
    }

    public async Task RecomputeGoalsAsync(IEnumerable<Guid> goalIds, DateOnly today, CancellationToken ct)
    {
        foreach (var goalId in goalIds.Distinct())
            await RecomputeGoalAsync(goalId, today, ct);
    }

    private async Task RecomputeGoalAsync(Guid goalId, DateOnly today, CancellationToken ct)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == goalId, ct);
        if (goal is null || goal.IsClosed)
            return;

        var planIds = await db.PlanGoalLinks.Where(l => l.GoalId == goalId).Select(l => l.PlanId).ToListAsync(ct);
        var plans = await db.Plans.Include(p => p.Criteria).Where(p => planIds.Contains(p.Id)).ToListAsync(ct);
        var sessions = await db.PlanOccurrences.Where(o => planIds.Contains(o.PlanId))
                                               .Select(o => new { o.Id, o.PlanId, o.ScheduledDate })
                                               .ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var planOfSession = sessions.ToDictionary(s => s.Id, s => s.PlanId);
        var logs = (await db.PlanLogs.Where(l => sessionIds.Contains(l.OccurrenceId))
                                     .Select(l => new { l.OccurrenceId, l.Completion, l.Score })
                                     .ToListAsync(ct))
                   .Select(l => (PlanId: planOfSession[l.OccurrenceId], l.Completion, l.Score))
                   .ToList();

        var planProgress = plans.ToDictionary(p => p.Id, p => PlanProgress(p, logs.Where(l => l.PlanId == p.Id).Select(l => l.Completion)));

        var milestones = await db.Milestones.Where(m => m.GoalId == goalId).ToListAsync(ct);
        var milestoneIds = milestones.Select(m => m.Id).ToHashSet();
        foreach (var milestone in milestones)
        {
            var own = plans.Where(p => p.MilestoneId == milestone.Id).Select(p => (planProgress[p.Id], 1m)).ToList();
            if (own.Count > 0)
                milestone.ApplyProgress(ProgressCalculator.WeightedAverage(own));
        }

        // Goal = weighted Milestones; plans outside any Milestone count as one more item each.
        var items = milestones.Select(m => (m.Progress, m.Weight))
            .Concat(plans.Where(p => p.MilestoneId is not Guid id || !milestoneIds.Contains(id)).Select(p => (planProgress[p.Id], 1m)))
            .ToList();
        decimal progress = ProgressCalculator.WeightedAverage(items);
        decimal expected = ProgressCalculator.Expected(goal.StartDate, goal.TargetDate, today);
        decimal? score = logs.Count > 0 ? Math.Round((decimal)logs.Average(l => l.Score), 1) : null;

        // A Goal is only judged once its plans have run long enough (BR-13); until then it stays Active.
        var firstSession = sessions.Count > 0 ? sessions.Min(s => s.ScheduledDate) : (DateOnly?)null;
        bool? atRisk = firstSession is { } first && today.DayNumber - first.DayNumber >= thresholds.MinDaysForTrends
            ? risk.EvaluateGoal(progress, expected).IsAtRisk
            : null;

        if (sessions.Count > 0)
            goal.Activate();
        goal.ApplyProgress(progress, expected, score, atRisk);
    }

    /// <summary>
    /// Plan progress (BRD §5.5): completion summed over the whole Duration. Achievement plans are the share of
    /// criteria done, since each log reports the cumulative state of the checklist.
    /// </summary>
    private static decimal PlanProgress(Plan plan, IEnumerable<decimal> completions) =>
        plan.PlanType == PlanType.Achievement
            ? plan.Criteria.Count == 0 ? 0 : plan.Criteria.Count(c => c.IsDone) * 100m / plan.Criteria.Count
            : ProgressCalculator.Plan(completions, ScheduleGenerator.CountSessions(plan.ToScheduleInput()));
}

/// <summary>XP totals and levels, per Area and overall (FR-12, FR-13).</summary>
internal static class XpTotals
{
    public static async Task<Dictionary<Guid, decimal>> PerAreaAsync(IAppDbContext db, CancellationToken ct) =>
        (await db.XpLedger.Select(x => new { x.AreaId, x.Amount }).ToListAsync(ct))
        .GroupBy(x => x.AreaId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

    /// <summary>Days with at least one Done session, for the streak (rest days are not modelled yet).</summary>
    public static async Task<List<DateOnly>> DoneDaysAsync(IAppDbContext db, DateOnly today, CancellationToken ct) =>
        await db.PlanOccurrences.Where(o => o.State == OccurrenceState.Done && o.ScheduledDate <= today)
                                .Select(o => o.ScheduledDate).Distinct().ToListAsync(ct);
}

/// <summary>Score result of one log, shown right after saving (FR-31) and when reopening a logged session.</summary>
public sealed record LogResultDto(
    Guid PlanLogId,
    Guid OccurrenceId,
    string PlanName,
    PlanType PlanType,
    Difficulty Difficulty,
    decimal Completion,
    decimal Quality,
    decimal Consistency,
    decimal DifficultyPoints,
    int Score,
    decimal Xp,
    decimal XpMultiplier,
    ScoreWeights Weights,
    string AreaName,
    string AreaColorHex,
    LevelInfo AreaLevel,
    LevelInfo OverallLevel,
    int CurrentStreak,
    DateTime LoggedAt,
    DateTime EditableUntil,
    bool CanEdit);

internal sealed class LogResultBuilder(IAppDbContext db, IClock clock, DifficultyTable difficulty, LevelTable levels)
{
    public async Task<LogResultDto?> BuildAsync(Guid planLogId, CancellationToken ct)
    {
        var log = await db.PlanLogs.FirstOrDefaultAsync(l => l.Id == planLogId, ct);
        if (log is null)
            return null;
        var occurrence = await db.PlanOccurrences.IgnoreQueryFilters().FirstAsync(o => o.Id == log.OccurrenceId, ct);
        var plan = await db.Plans.IgnoreQueryFilters().Include(p => p.GoalLinks).FirstAsync(p => p.Id == occurrence.PlanId, ct);
        var area = await PrimaryAreaAsync(db, plan, ct);

        var xpPerArea = await XpTotals.PerAreaAsync(db, ct);
        var doneDays = await XpTotals.DoneDaysAsync(db, clock.Today, ct);

        return new LogResultDto(
            log.Id, occurrence.Id, plan.Name, plan.PlanType, plan.Difficulty,
            log.Completion, log.QualityPct, log.Consistency, log.DifficultyPts, log.Score, log.Xp,
            difficulty.XpMultiplier(plan.Difficulty), ScoreWeights.FromVersion(log.ScoreVersion),
            area?.Name ?? "", area?.ColorHex ?? "#6B7280",
            levels.For(area is null ? 0 : xpPerArea.GetValueOrDefault(area.Id)),
            levels.For(xpPerArea.Values.Sum()),
            StreakCalculator.Calculate(doneDays, [], clock.Today).Current,
            log.LoggedAt, log.EditableUntil, log.CanEdit(clock.UtcNow));
    }

    /// <summary>The Area of the Goal the plan contributes to most.</summary>
    public static async Task<Domain.Areas.Area?> PrimaryAreaAsync(IAppDbContext db, Plan plan, CancellationToken ct)
    {
        var goalId = plan.GoalLinks.OrderByDescending(l => l.Weight).Select(l => l.GoalId).FirstOrDefault();
        var areaId = await db.Goals.IgnoreQueryFilters().Where(g => g.Id == goalId).Select(g => (Guid?)g.AreaId).FirstOrDefaultAsync(ct);
        return areaId is null ? null : await db.Areas.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == areaId, ct);
    }
}
