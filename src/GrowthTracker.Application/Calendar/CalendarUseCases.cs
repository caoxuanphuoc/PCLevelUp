using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Calendar;

// ── GetCalendar ─────────────────────────────────────────────────────────────

/// <summary>
/// Sessions, deadlines and schedule metrics for a date range (FR-21, FR-22, FR-25, BRD §7). One query serves the
/// month, week and list views (TDD §3.1 GetMonthView / GetWeekView); the view just picks the range.
/// </summary>
public sealed record GetCalendarQuery(DateOnly From, DateOnly To, Guid? AreaId = null) : IQuery<CalendarDto>;

public sealed record CalendarDto(
    DateOnly From,
    DateOnly To,
    DateOnly Today,
    IReadOnlyList<CalendarSessionDto> Sessions,
    IReadOnlyList<DeadlineDto> Deadlines,
    IReadOnlySet<DateOnly> PeakDays,
    CalendarStatsDto Stats,
    IReadOnlyList<CalendarAreaDto> Areas);

public sealed record CalendarSessionDto(
    Guid OccurrenceId,
    Guid PlanId,
    string PlanName,
    PlanType PlanType,
    Guid AreaId,
    string AreaName,
    string AreaColorHex,
    DateOnly Date,
    TimeOnly? Time,
    // Planned minutes: the target of Time plans, otherwise a nominal 30 minutes.
    int DurationMinutes,
    OccurrenceState State,
    int? Score,
    DateOnly OriginalDate,
    int RescheduleCount)
{
    public bool IsMoved => Date != OriginalDate;
}

public enum DeadlineKind
{
    Milestone,
    Goal,
}

/// <summary>A Milestone or Goal due date shown on the calendar (FR-22).</summary>
public sealed record DeadlineDto(DeadlineKind Kind, Guid GoalId, string Name, string GoalName, DateOnly Date,
                                 string AreaColorHex, bool IsAtRisk, bool IsCompleted);

/// <summary>An Area filter chip with the number of sessions it has in the range.</summary>
public sealed record CalendarAreaDto(Guid AreaId, string Name, string ColorHex, int SessionCount);

/// <param name="RescheduleRatePct">Moved sessions ÷ scheduled sessions in the range (BRD §7).</param>
/// <param name="AdherencePct">Sessions done on their scheduled day ÷ sessions due in the range; null when none are due yet.</param>
/// <param name="BestWeekday">Weekday with the highest average Score; needs logs in at least 4 different weeks (BR-13).</param>
public sealed record CalendarStatsDto(
    int SessionCount,
    int DoneCount,
    int DueCount,
    decimal RescheduleRatePct,
    decimal? AdherencePct,
    DayOfWeek? BestWeekday,
    decimal? BestWeekdayScore,
    int DeadlineCount,
    int AtRiskDeadlines);

internal sealed class GetCalendarHandler(IAppDbContext db, IClock clock) : IQueryHandler<GetCalendarQuery, CalendarDto>
{
    /// <summary>A day is a peak when its planned minutes exceed 1.5× the average day (FR-25).</summary>
    public const decimal PeakFactor = 1.5m;
    public const int NominalMinutes = 30;

    public async Task<CalendarDto> HandleAsync(GetCalendarQuery q, CancellationToken ct)
    {
        var today = clock.Today;
        var areas = await db.Areas.IgnoreQueryFilters().ToDictionaryAsync(a => a.Id, ct);
        var goals = await db.Goals.ToDictionaryAsync(g => g.Id, ct);
        var links = await db.PlanGoalLinks.ToListAsync(ct);
        var plans = await db.Plans.IgnoreQueryFilters().ToDictionaryAsync(p => p.Id, ct);

        // The Area of a plan is the Area of the Goal it contributes to most.
        Guid? AreaOf(Guid planId) => links.Where(l => l.PlanId == planId && goals.ContainsKey(l.GoalId))
                                          .OrderByDescending(l => l.Weight)
                                          .Select(l => (Guid?)goals[l.GoalId].AreaId)
                                          .FirstOrDefault();

        var rows = await db.PlanOccurrences.Where(o => o.ScheduledDate >= q.From && o.ScheduledDate <= q.To).ToListAsync(ct);
        var rowIds = rows.Select(o => o.Id).ToList();
        var logs = await db.PlanLogs.Where(l => rowIds.Contains(l.OccurrenceId))
                                    .Select(l => new { l.OccurrenceId, l.Score, l.LoggedAt })
                                    .ToDictionaryAsync(l => l.OccurrenceId, ct);

        var all = rows
            .Select(o => (Occurrence: o, Plan: plans.GetValueOrDefault(o.PlanId), AreaId: AreaOf(o.PlanId)))
            .Where(x => x.Plan is not null && x.AreaId is Guid id && areas.ContainsKey(id))
            .Select(x =>
            {
                var area = areas[x.AreaId!.Value];
                var o = x.Occurrence;
                return new CalendarSessionDto(
                    o.Id, x.Plan!.Id, x.Plan.Name, x.Plan.PlanType, area.Id, area.Name, area.ColorHex,
                    o.ScheduledDate, o.ScheduledTime,
                    x.Plan.PlanType == PlanType.Time && x.Plan.TargetValue > 0 ? (int)x.Plan.TargetValue : NominalMinutes,
                    o.State, logs.TryGetValue(o.Id, out var log) ? log.Score : null, o.OriginalDate, o.RescheduleCount);
            })
            .ToList();

        var areaChips = all.GroupBy(s => s.AreaId)
            .Select(g => new CalendarAreaDto(g.Key, areas[g.Key].Name, areas[g.Key].ColorHex, g.Count()))
            .OrderBy(a => areas[a.AreaId].SortOrder)
            .ToList();

        var sessions = all.Where(s => q.AreaId is null || s.AreaId == q.AreaId)
                          .OrderBy(s => s.Date).ThenBy(s => s.Time ?? TimeOnly.MaxValue).ThenBy(s => s.PlanName)
                          .ToList();

        // Deadlines (FR-22): Goal target dates and Milestone due dates in the range.
        var milestones = await db.Milestones.Where(m => m.DueDate != null && m.DueDate >= q.From && m.DueDate <= q.To).ToListAsync(ct);
        var deadlines = goals.Values
            .Where(g => g.TargetDate >= q.From && g.TargetDate <= q.To && g.Status != GoalStatus.Cancelled)
            .Select(g => new DeadlineDto(DeadlineKind.Goal, g.Id, g.Name, g.Name, g.TargetDate, ColorOf(g.AreaId),
                                         g.Status == GoalStatus.AtRisk, g.Status == GoalStatus.Completed))
            .Concat(milestones.Where(m => goals.ContainsKey(m.GoalId)).Select(m =>
            {
                var goal = goals[m.GoalId];
                return new DeadlineDto(DeadlineKind.Milestone, goal.Id, m.Name, goal.Name, m.DueDate!.Value, ColorOf(goal.AreaId),
                                       goal.Status == GoalStatus.AtRisk && m.Status != Domain.Milestones.MilestoneStatus.Completed,
                                       m.Status == Domain.Milestones.MilestoneStatus.Completed);
            }))
            .Where(d => q.AreaId is null || goals[d.GoalId].AreaId == q.AreaId)
            .OrderBy(d => d.Date)
            .ToList();

        string ColorOf(Guid areaId) => areas.TryGetValue(areaId, out var a) ? a.ColorHex : "#6B7280";

        return new CalendarDto(q.From, q.To, today, sessions, deadlines, PeakDays(sessions),
                               await StatsAsync(sessions, deadlines, logs.ToDictionary(l => l.Key, l => l.Value.LoggedAt), today, ct),
                               areaChips);
    }

    private static IReadOnlySet<DateOnly> PeakDays(IReadOnlyList<CalendarSessionDto> sessions)
    {
        var minutesPerDay = sessions.Where(s => s.State != OccurrenceState.Skipped)
                                    .GroupBy(s => s.Date)
                                    .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationMinutes));
        if (minutesPerDay.Count < 2)
            return new HashSet<DateOnly>();
        decimal average = (decimal)minutesPerDay.Values.Average();
        return minutesPerDay.Where(d => d.Value > average * PeakFactor).Select(d => d.Key).ToHashSet();
    }

    private async Task<CalendarStatsDto> StatsAsync(IReadOnlyList<CalendarSessionDto> sessions, IReadOnlyList<DeadlineDto> deadlines,
                                                    IReadOnlyDictionary<Guid, DateTime> loggedAt, DateOnly today, CancellationToken ct)
    {
        var due = sessions.Where(s => s.Date <= today).ToList();
        // "Done on the scheduled day": never moved, and logged on that local date.
        int onTime = due.Count(s => s.State == OccurrenceState.Done && !s.IsMoved
                                    && loggedAt.TryGetValue(s.OccurrenceId, out var at) && LocalDate(at) == s.Date);

        // Best weekday over all history (BRD §7), only once there are 4 weeks of logs (BR-13).
        var history = await (from l in db.PlanLogs
                             join o in db.PlanOccurrences on l.OccurrenceId equals o.Id
                             select new { o.ScheduledDate, l.Score }).ToListAsync(ct);
        DayOfWeek? best = null;
        decimal? bestScore = null;
        int weeksWithLogs = history.Select(h => h.ScheduledDate.AddDays(-(((int)h.ScheduledDate.DayOfWeek + 6) % 7))).Distinct().Count();
        if (weeksWithLogs >= 4)
        {
            var top = history.GroupBy(h => h.ScheduledDate.DayOfWeek)
                             .Select(g => (Day: g.Key, Avg: (decimal)g.Average(h => h.Score)))
                             .OrderByDescending(x => x.Avg).First();
            (best, bestScore) = (top.Day, Math.Round(top.Avg, 1));
        }

        return new CalendarStatsDto(
            sessions.Count,
            sessions.Count(s => s.State == OccurrenceState.Done),
            due.Count,
            sessions.Count == 0 ? 0 : Math.Round(sessions.Count(s => s.IsMoved) * 100m / sessions.Count, 1),
            due.Count == 0 ? null : Math.Round(onTime * 100m / due.Count, 1),
            best, bestScore,
            deadlines.Count, deadlines.Count(d => d.IsAtRisk));
    }

    private DateOnly LocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), clock.TimeZone));
}

// ── RescheduleOccurrence ────────────────────────────────────────────────────

public enum RescheduleScope
{
    /// <summary>Only this session.</summary>
    ThisOnly,
    /// <summary>This session and every later scheduled session of the plan, shifted by the same number of days.</summary>
    ThisAndFollowing,
}

/// <summary>
/// Moves a session to another day (FR-23), optionally with the plan's later sessions. Never into the past (FR-24)
/// and never changes a Score (BR-12); each moved session counts toward the Reschedule Rate.
/// Returns how many sessions moved.
/// </summary>
public sealed record RescheduleOccurrenceCommand(Guid OccurrenceId, DateOnly NewDate, RescheduleScope Scope = RescheduleScope.ThisOnly,
                                                 string? Reason = null, TimeOnly? NewTime = null) : ICommand<int>;

internal sealed class RescheduleOccurrenceHandler(IAppDbContext db, IClock clock) : ICommandHandler<RescheduleOccurrenceCommand, int>
{
    public async Task<Result<int>> HandleAsync(RescheduleOccurrenceCommand c, CancellationToken ct)
    {
        var occurrence = await db.PlanOccurrences.FirstOrDefaultAsync(o => o.Id == c.OccurrenceId, ct);
        if (occurrence is null)
            return Result<int>.Failure(Errors.NotFound("occurrence"));

        var today = clock.Today;
        var now = clock.UtcNow;
        string? reason = string.IsNullOrWhiteSpace(c.Reason) ? null : c.Reason.Trim();
        int offset = c.NewDate.DayNumber - occurrence.ScheduledDate.DayNumber;

        var toMove = c.Scope == RescheduleScope.ThisOnly
            ? [occurrence]
            : await db.PlanOccurrences
                .Where(o => o.PlanId == occurrence.PlanId && o.State == OccurrenceState.Scheduled && o.ScheduledDate >= occurrence.ScheduledDate)
                .OrderBy(o => o.ScheduledDate)
                .ToListAsync(ct);
        if (!toMove.Contains(occurrence))
            toMove.Insert(0, occurrence); // let the domain reject a non-scheduled session with its own error

        int moved = 0;
        foreach (var session in toMove)
        {
            var target = session.Id == occurrence.Id ? c.NewDate : session.ScheduledDate.AddDays(offset);
            var before = (session.ScheduledDate, session.ScheduledTime);
            session.Reschedule(target, today, reason, now, session.Id == occurrence.Id ? c.NewTime : null);
            if ((session.ScheduledDate, session.ScheduledTime) != before)
                moved++;
        }
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(moved);
    }
}

// ── AddSession ──────────────────────────────────────────────────────────────

/// <summary>Adds one extra session of an active plan on a day (TDD §3.1 AddPlanToCalendar); not in the past (FR-24).</summary>
public sealed record AddSessionCommand(Guid PlanId, DateOnly Date, TimeOnly? Time = null) : ICommand<Guid>;

internal sealed class AddSessionHandler(IAppDbContext db, IClock clock) : ICommandHandler<AddSessionCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(AddSessionCommand c, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == c.PlanId, ct);
        if (plan is null)
            return Result<Guid>.Failure(Errors.NotFound("plan"));
        if (plan.Status != PlanStatus.Active)
            return Result<Guid>.Failure("plan.not_active", "Only active plans can get new sessions.");
        if (c.Date < clock.Today)
            return Result<Guid>.Failure("occurrence.past_date", "Sessions cannot be scheduled in the past (FR-24).");

        var session = new PlanOccurrence(plan.Id, c.Date, c.Time ?? plan.PreferredTime);
        db.PlanOccurrences.Add(session);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(session.Id);
    }
}

// ── GetSchedulablePlans ─────────────────────────────────────────────────────

public sealed record SchedulablePlanDto(Guid PlanId, string Name, string GoalName, string AreaColorHex, TimeOnly? PreferredTime);

/// <summary>Active plans the user can add a session for, for the "add to calendar" picker.</summary>
public sealed record GetSchedulablePlansQuery : IQuery<IReadOnlyList<SchedulablePlanDto>>;

internal sealed class GetSchedulablePlansHandler(IAppDbContext db) : IQueryHandler<GetSchedulablePlansQuery, IReadOnlyList<SchedulablePlanDto>>
{
    public async Task<IReadOnlyList<SchedulablePlanDto>> HandleAsync(GetSchedulablePlansQuery query, CancellationToken ct)
    {
        var plans = await db.Plans.Include(p => p.GoalLinks).Where(p => p.Status == PlanStatus.Active).ToListAsync(ct);
        var goals = await db.Goals.ToDictionaryAsync(g => g.Id, ct);
        var areas = await db.Areas.IgnoreQueryFilters().ToDictionaryAsync(a => a.Id, a => a.ColorHex, ct);
        return plans
            .Select(p => (Plan: p, Goal: goals.GetValueOrDefault(p.GoalLinks.OrderByDescending(l => l.Weight).First().GoalId)))
            .Where(x => x.Goal is not null && !x.Goal.IsClosed)
            .Select(x => new SchedulablePlanDto(x.Plan.Id, x.Plan.Name, x.Goal!.Name, areas.GetValueOrDefault(x.Goal.AreaId, "#6B7280"),
                                                x.Plan.PreferredTime))
            .OrderBy(p => p.GoalName).ThenBy(p => p.Name)
            .ToList();
    }
}
