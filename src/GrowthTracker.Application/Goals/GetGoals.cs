using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Progress;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Goals;

/// <param name="AreaId">Only this Area; null for all.</param>
/// <param name="Statuses">Statuses to show; null shows everything except Cancelled (UI Design §6.1).</param>
public sealed record GetGoalsQuery(Guid? AreaId = null, IReadOnlyCollection<GoalStatus>? Statuses = null) : IQuery<GoalsPageDto>;

public sealed record GoalsPageDto(GoalsSummaryDto Summary, IReadOnlyList<AreaFilterDto> AreaFilters, IReadOnlyList<GoalCardDto> Goals);

/// <summary>Header figures, always over all open (not completed/cancelled) Goals regardless of filters.</summary>
public sealed record GoalsSummaryDto(int OpenGoals, int AreasWithGoals, int AtRisk, int OnTrack, int ActivePlans);

/// <summary>An Area tab with the number of Goals it would show under the current status filter.</summary>
public sealed record AreaFilterDto(Guid AreaId, string Name, string ColorHex, int GoalCount);

public sealed record GoalCardDto(
    Guid Id,
    string Name,
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
    string? CurrentMilestone,
    int MilestoneCount,
    int ActivePlanCount);

internal sealed class GetGoalsHandler(IAppDbContext db, IClock clock) : IQueryHandler<GetGoalsQuery, GoalsPageDto>
{
    public async Task<GoalsPageDto> HandleAsync(GetGoalsQuery query, CancellationToken ct)
    {
        var today = clock.Today;
        var areas = await db.Areas.OrderBy(a => a.SortOrder).ToListAsync(ct);
        var areaById = areas.ToDictionary(a => a.Id);
        var goals = await db.Goals.ToListAsync(ct);

        var activePlansPerGoal = await (
                from link in db.PlanGoalLinks
                join plan in db.Plans on link.PlanId equals plan.Id
                where plan.Status == PlanStatus.Active
                group link by link.GoalId into g
                select new { GoalId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GoalId, x => x.Count, ct);
        int activePlans = await db.Plans.CountAsync(p => p.Status == PlanStatus.Active, ct);

        var milestones = (await db.Milestones.ToListAsync(ct))
            .GroupBy(m => m.GoalId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.SortOrder).ToList());

        var open = goals.Where(g => !g.IsClosed).ToList();
        var summary = new GoalsSummaryDto(
            open.Count,
            open.Select(g => g.AreaId).Distinct().Count(),
            open.Count(g => g.Status == GoalStatus.AtRisk),
            open.Count(g => g.Status == GoalStatus.OnTrack),
            activePlans);

        bool StatusMatches(Goal g) => query.Statuses is { Count: > 0 } statuses
            ? statuses.Contains(g.Status)
            : g.Status != GoalStatus.Cancelled;

        var statusFiltered = goals.Where(StatusMatches).ToList();

        var areaFilters = areas
            .Where(a => !a.IsArchived || statusFiltered.Any(g => g.AreaId == a.Id))
            .Select(a => new AreaFilterDto(a.Id, a.Name, a.ColorHex, statusFiltered.Count(g => g.AreaId == a.Id)))
            .ToList();

        var cards = statusFiltered
            .Where(g => query.AreaId is null || g.AreaId == query.AreaId)
            .Where(g => areaById.ContainsKey(g.AreaId))
            // UI Design §6.1: At Risk first, then Priority, then nearest deadline.
            .OrderByDescending(g => g.Status == GoalStatus.AtRisk)
            .ThenBy(g => g.Priority)
            .ThenBy(g => g.TargetDate)
            .Select(g =>
            {
                var area = areaById[g.AreaId];
                var goalMilestones = milestones.GetValueOrDefault(g.Id) ?? [];
                return new GoalCardDto(
                    g.Id, g.Name, area.Id, area.Name, area.ColorHex, g.Status, g.Priority,
                    g.StartDate, g.TargetDate,
                    DaysRemaining: Math.Max(0, g.TargetDate.DayNumber - today.DayNumber),
                    g.Progress,
                    ProgressCalculator.Expected(g.StartDate, g.TargetDate, today),
                    g.Score,
                    goalMilestones.FirstOrDefault(m => m.Status != MilestoneStatus.Completed)?.Name,
                    goalMilestones.Count,
                    activePlansPerGoal.GetValueOrDefault(g.Id));
            })
            .ToList();

        return new GoalsPageDto(summary, areaFilters, cards);
    }
}
