using System.Globalization;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Gamification;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Logging;

// ── GetToday ────────────────────────────────────────────────────────────────

/// <summary>Everything the Today screen shows (FR-30, UI Design §7).</summary>
public sealed record GetTodayQuery : IQuery<TodayDto>;

public sealed record TodayDto(
    DateOnly Date,
    int IsoWeek,
    int IsoYear,
    Streak Streak,
    LevelInfo OverallLevel,
    decimal XpToday,
    IReadOnlyList<TodaySessionDto> Sessions,
    IReadOnlyList<WeekDayDto> Week,
    IReadOnlyList<AreaLevelDto> Areas)
{
    public int DoneCount => Sessions.Count(s => s.State == OccurrenceState.Done);
    /// <summary>Sessions the user can still act on or has acted on; skipped ones are excluded from the day's goal.</summary>
    public int PlannedCount => Sessions.Count(s => s.State != OccurrenceState.Skipped);
}

public sealed record TodaySessionDto(
    Guid OccurrenceId,
    Guid PlanId,
    string PlanName,
    PlanType PlanType,
    decimal TargetValue,
    string? TargetUnit,
    int? FrequencyPerWeek,
    Difficulty Difficulty,
    TimeOnly? ScheduledTime,
    OccurrenceState State,
    SkipReason? SkipReason,
    string AreaName,
    string AreaColorHex,
    string? GoalName,
    int CriteriaDone,
    int CriteriaTotal,
    LoggedSessionDto? Log);

public sealed record LoggedSessionDto(Guid PlanLogId, decimal ActualValue, int Quality, int Score, decimal Xp,
                                      DateTime LoggedAt, DateTime EditableUntil, bool CanEdit);

/// <summary>One day of the current week: done/planned sessions and the average Score of its logs.</summary>
public sealed record WeekDayDto(DateOnly Date, int Done, int Planned, decimal? AverageScore);

public sealed record AreaLevelDto(Guid AreaId, string Name, string ColorHex, LevelInfo Level);

internal sealed class GetTodayHandler(IAppDbContext db, IClock clock, LevelTable levels) : IQueryHandler<GetTodayQuery, TodayDto>
{
    public async Task<TodayDto> HandleAsync(GetTodayQuery query, CancellationToken ct)
    {
        var today = clock.Today;
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday
        var weekEnd = weekStart.AddDays(6);

        var weekSessions = await db.PlanOccurrences
            .Where(o => o.ScheduledDate >= weekStart && o.ScheduledDate <= weekEnd)
            .ToListAsync(ct);
        var weekIds = weekSessions.Select(o => o.Id).ToList();
        var logs = await db.PlanLogs.Where(l => weekIds.Contains(l.OccurrenceId)).ToListAsync(ct);
        var logBySession = logs.ToDictionary(l => l.OccurrenceId);

        var todaySessions = weekSessions.Where(o => o.ScheduledDate == today).ToList();
        var planIds = todaySessions.Select(o => o.PlanId).Distinct().ToList();
        var plans = await db.Plans.IgnoreQueryFilters().Include(p => p.Criteria).Include(p => p.GoalLinks)
                                  .Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var goalIds = plans.Values.SelectMany(p => p.GoalLinks.Select(l => l.GoalId)).Distinct().ToList();
        var goals = await db.Goals.IgnoreQueryFilters().Where(g => goalIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, ct);
        var areas = await db.Areas.IgnoreQueryFilters().ToDictionaryAsync(a => a.Id, ct);

        var now = clock.UtcNow;
        var sessions = todaySessions
            .Select(o =>
            {
                var plan = plans[o.PlanId];
                var primaryGoalId = plan.GoalLinks.OrderByDescending(l => l.Weight).First().GoalId;
                var goal = goals.GetValueOrDefault(primaryGoalId);
                var area = goal is null ? null : areas.GetValueOrDefault(goal.AreaId);
                var log = logBySession.GetValueOrDefault(o.Id);
                return new TodaySessionDto(
                    o.Id, plan.Id, plan.Name, plan.PlanType, plan.TargetValue, plan.TargetUnit, plan.FrequencyPerWeek,
                    plan.Difficulty, o.ScheduledTime, o.State, o.SkipReason,
                    area?.Name ?? "", area?.ColorHex ?? "#6B7280", goal?.Name,
                    plan.Criteria.Count(c => c.IsDone), plan.Criteria.Count,
                    log is null ? null : new LoggedSessionDto(log.Id, log.ActualValue, log.Quality, log.Score, log.Xp,
                                                              log.LoggedAt, log.EditableUntil, log.CanEdit(now)));
            })
            .OrderBy(s => s.ScheduledTime ?? TimeOnly.MaxValue)
            .ThenBy(s => s.PlanName)
            .ToList();

        var week = Enumerable.Range(0, 7).Select(i =>
        {
            var date = weekStart.AddDays(i);
            var day = weekSessions.Where(o => o.ScheduledDate == date).ToList();
            var scores = day.Select(o => logBySession.GetValueOrDefault(o.Id)).Where(l => l is not null).Select(l => l!.Score).ToList();
            return new WeekDayDto(date, day.Count(o => o.State == OccurrenceState.Done),
                                  day.Count(o => o.State != OccurrenceState.Skipped),
                                  scores.Count > 0 ? (decimal)scores.Average() : null);
        }).ToList();

        var xpPerArea = await XpTotals.PerAreaAsync(db, ct);
        var areaLevels = areas.Values.Where(a => !a.IsArchived || xpPerArea.ContainsKey(a.Id))
            .OrderBy(a => a.SortOrder)
            .Select(a => new AreaLevelDto(a.Id, a.Name, a.ColorHex, levels.For(xpPerArea.GetValueOrDefault(a.Id))))
            .ToList();

        var doneDays = await XpTotals.DoneDaysAsync(db, today, ct);

        return new TodayDto(
            today, ISOWeek.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue)), ISOWeek.GetYear(today.ToDateTime(TimeOnly.MinValue)),
            StreakCalculator.Calculate(doneDays, [], today),
            levels.For(xpPerArea.Values.Sum()),
            sessions.Where(s => s.Log is not null).Sum(s => s.Log!.Xp),
            sessions, week, areaLevels);
    }
}

// ── GetPlanResultForm ───────────────────────────────────────────────────────

/// <summary>Data for the Plan Result dialog: the plan, its target, its criteria, and the existing log when editing.</summary>
public sealed record GetPlanResultFormQuery(Guid OccurrenceId) : IQuery<PlanResultFormDto?>;

public sealed record PlanResultFormDto(
    Guid OccurrenceId,
    DateOnly ScheduledDate,
    OccurrenceState State,
    Guid PlanId,
    string PlanName,
    PlanType PlanType,
    decimal TargetValue,
    string? TargetUnit,
    int? FrequencyPerWeek,
    Difficulty Difficulty,
    decimal XpMultiplier,
    ScoreWeights Weights,
    string AreaName,
    string AreaColorHex,
    string? GoalName,
    IReadOnlyList<CriterionDto> Criteria,
    LoggedSessionDto? Log,
    string? Note,
    // False for future sessions (FR-24).
    bool CanLog);

public sealed record CriterionDto(Guid Id, string Title, bool IsDone);

internal sealed class GetPlanResultFormHandler(IAppDbContext db, IClock clock, DifficultyTable difficulty, ScoreEngine engine)
    : IQueryHandler<GetPlanResultFormQuery, PlanResultFormDto?>
{
    public async Task<PlanResultFormDto?> HandleAsync(GetPlanResultFormQuery query, CancellationToken ct)
    {
        var occurrence = await db.PlanOccurrences.FirstOrDefaultAsync(o => o.Id == query.OccurrenceId, ct);
        if (occurrence is null)
            return null;
        var plan = await db.Plans.IgnoreQueryFilters().Include(p => p.Criteria).Include(p => p.GoalLinks)
                                 .FirstAsync(p => p.Id == occurrence.PlanId, ct);
        var area = await LogResultBuilder.PrimaryAreaAsync(db, plan, ct);
        var goalId = plan.GoalLinks.OrderByDescending(l => l.Weight).First().GoalId;
        var goalName = await db.Goals.IgnoreQueryFilters().Where(g => g.Id == goalId).Select(g => g.Name).FirstOrDefaultAsync(ct);
        var log = await db.PlanLogs.FirstOrDefaultAsync(l => l.OccurrenceId == occurrence.Id, ct);
        var now = clock.UtcNow;

        return new PlanResultFormDto(
            occurrence.Id, occurrence.ScheduledDate, occurrence.State, plan.Id, plan.Name, plan.PlanType,
            plan.TargetValue, plan.TargetUnit, plan.FrequencyPerWeek, plan.Difficulty,
            difficulty.XpMultiplier(plan.Difficulty), engine.Weights,
            area?.Name ?? "", area?.ColorHex ?? "#6B7280", goalName,
            plan.Criteria.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Select(c => new CriterionDto(c.Id, c.Title, c.IsDone)).ToList(),
            log is null ? null : new LoggedSessionDto(log.Id, log.ActualValue, log.Quality, log.Score, log.Xp,
                                                      log.LoggedAt, log.EditableUntil, log.CanEdit(now)),
            log?.Note,
            CanLog: occurrence.ScheduledDate <= clock.Today);
    }
}
