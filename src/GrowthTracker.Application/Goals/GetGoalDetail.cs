using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Progress;
using GrowthTracker.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Goals;

public sealed record GetGoalDetailQuery(Guid GoalId) : IQuery<GoalDetailDto?>;

public sealed record GoalDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string? TargetDescription,
    Guid AreaId,
    string AreaName,
    string AreaColorHex,
    GoalStatus Status,
    int Priority,
    DateOnly StartDate,
    DateOnly TargetDate,
    int DaysRemaining,
    decimal Progress,
    decimal ExpectedProgress,
    decimal? Score,
    IReadOnlyList<MilestoneDto> Milestones,
    // Plans linked to the Goal but not to any of its Milestones.
    IReadOnlyList<PlanSummaryDto> UnassignedPlans)
{
    public int PlanCount => Milestones.Sum(m => m.Plans.Count) + UnassignedPlans.Count;
}

public sealed record MilestoneDto(
    Guid Id,
    string Name,
    DateOnly? DueDate,
    decimal Weight,
    int SortOrder,
    MilestoneStatus Status,
    decimal Progress,
    IReadOnlyList<PlanSummaryDto> Plans,
    // Past its due date and not completed.
    bool IsOverdue = false);

public sealed record PlanSummaryDto(
    Guid Id,
    string Name,
    PlanType PlanType,
    decimal TargetValue,
    string? TargetUnit,
    int? FrequencyPerWeek,
    // Days the plan is actually scheduled on (explicit days, or derived from frequency).
    WeekDays ScheduledDays,
    TimeOnly? PreferredTime,
    DateOnly DurationStart,
    DateOnly DurationEnd,
    Difficulty Difficulty,
    PlanStatus Status,
    DateOnly? NextSession,
    int UpcomingSessions,
    int SessionsDone,
    int SessionsDue,
    decimal Progress,
    decimal? AverageScore,
    int CriteriaDone,
    int CriteriaTotal,
    int LinkedGoalCount,
    // FR-51: Consistency over the last 14 days below 50% (with at least 7 days of sessions).
    bool MayBeTooHard = false,
    decimal? Consistency14Days = null,
    // Active but past its end date: shown as "Đã kết thúc"; extending the end date brings it back.
    bool HasEnded = false,
    // The days chosen in the editor (None = spread automatically), and the plan's milestone, for "Sửa kế hoạch".
    WeekDays DaysOfWeek = WeekDays.None,
    Guid? MilestoneId = null);

internal sealed class GetGoalDetailHandler(IAppDbContext db, IClock clock, Domain.Risk.RiskDetector risk, Domain.Risk.DomainThresholds thresholds)
    : IQueryHandler<GetGoalDetailQuery, GoalDetailDto?>
{
    public async Task<GoalDetailDto?> HandleAsync(GetGoalDetailQuery query, CancellationToken ct)
    {
        var today = clock.Today;
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == query.GoalId, ct);
        if (goal is null)
            return null;
        var area = await db.Areas.IgnoreQueryFilters().FirstAsync(a => a.Id == goal.AreaId, ct);

        var milestones = await db.Milestones.Where(m => m.GoalId == goal.Id).OrderBy(m => m.SortOrder).ToListAsync(ct);

        var planIds = await db.PlanGoalLinks.Where(l => l.GoalId == goal.Id).Select(l => l.PlanId).ToListAsync(ct);
        var plans = await db.Plans.Include(p => p.Criteria).Include(p => p.GoalLinks)
                                  .Where(p => planIds.Contains(p.Id))
                                  .OrderBy(p => p.Status).ThenBy(p => p.CreatedAt)
                                  .ToListAsync(ct);

        var occurrences = await db.PlanOccurrences.Where(o => planIds.Contains(o.PlanId)).ToListAsync(ct);
        var occurrenceIds = occurrences.Select(o => o.Id).ToList();
        var logs = await db.PlanLogs.Where(l => occurrenceIds.Contains(l.OccurrenceId))
                                    .Select(l => new { l.OccurrenceId, l.Completion, l.Score })
                                    .ToListAsync(ct);
        var planOfOccurrence = occurrences.ToDictionary(o => o.Id, o => o.PlanId);
        var logsByPlan = logs.GroupBy(l => planOfOccurrence[l.OccurrenceId]).ToDictionary(g => g.Key, g => g.ToList());
        var occurrencesByPlan = occurrences.GroupBy(o => o.PlanId).ToDictionary(g => g.Key, g => g.ToList());

        PlanSummaryDto Summarize(Plan p)
        {
            var sessions = occurrencesByPlan.GetValueOrDefault(p.Id) ?? [];
            var planLogs = logsByPlan.GetValueOrDefault(p.Id);
            var upcoming = sessions.Where(o => o.State == OccurrenceState.Scheduled && o.ScheduledDate >= today)
                                   .OrderBy(o => o.ScheduledDate).ToList();
            var schedule = p.ToScheduleInput();
            var windowStart = today.AddDays(-(thresholds.PlanRiskWindowDays - 1));
            var window = sessions.Where(o => o.ScheduledDate >= windowStart && o.ScheduledDate <= today).ToList();
            var planRisk = risk.EvaluatePlan(window.Count(o => o.State == OccurrenceState.Done), window.Count,
                sessions.Count == 0 ? 0 : Math.Min(thresholds.PlanRiskWindowDays, today.DayNumber - sessions.Min(o => o.ScheduledDate).DayNumber + 1));
            return new PlanSummaryDto(
                p.Id, p.Name, p.PlanType, p.TargetValue, p.TargetUnit, p.FrequencyPerWeek,
                ScheduleGenerator.EffectiveDays(schedule), p.PreferredTime, p.DurationStart, p.DurationEnd,
                p.Difficulty, p.Status,
                NextSession: upcoming.FirstOrDefault()?.ScheduledDate,
                UpcomingSessions: upcoming.Count,
                SessionsDone: sessions.Count(o => o.State == OccurrenceState.Done),
                SessionsDue: sessions.Count(o => o.ScheduledDate <= today),
                Progress: ProgressCalculator.Plan(planLogs?.Select(l => l.Completion) ?? [], ScheduleGenerator.CountSessions(schedule)),
                AverageScore: planLogs is { Count: > 0 } ? (decimal)planLogs.Average(l => l.Score) : null,
                CriteriaDone: p.Criteria.Count(c => c.IsDone),
                CriteriaTotal: p.Criteria.Count,
                LinkedGoalCount: p.GoalLinks.Count,
                MayBeTooHard: p.Status == PlanStatus.Active && !p.HasEnded(today) && planRisk.IsTooHard,
                Consistency14Days: window.Count == 0 ? null : Math.Round(planRisk.ConsistencyPct, 1),
                HasEnded: p.HasEnded(today),
                DaysOfWeek: p.DaysOfWeek,
                MilestoneId: p.MilestoneId);
        }

        var milestoneIds = milestones.Select(m => m.Id).ToHashSet();
        var summaries = plans.Select(p => (Plan: p, Dto: Summarize(p))).ToList();

        var milestoneDtos = milestones.Select(m => new MilestoneDto(
            m.Id, m.Name, m.DueDate, m.Weight, m.SortOrder, m.Status, m.Progress,
            summaries.Where(s => s.Plan.MilestoneId == m.Id).Select(s => s.Dto).ToList(),
            IsOverdue: m.Status != MilestoneStatus.Completed && m.DueDate < today)).ToList();
        var unassigned = summaries.Where(s => s.Plan.MilestoneId is not Guid id || !milestoneIds.Contains(id))
                                  .Select(s => s.Dto).ToList();

        return new GoalDetailDto(
            goal.Id, goal.Name, goal.Description, goal.TargetDescription,
            area.Id, area.Name, area.ColorHex, goal.Status, goal.Priority, goal.StartDate, goal.TargetDate,
            DaysRemaining: Math.Max(0, goal.TargetDate.DayNumber - today.DayNumber),
            goal.Progress,
            ProgressCalculator.Expected(goal.StartDate, goal.TargetDate, today),
            goal.Score,
            milestoneDtos,
            unassigned);
    }
}
