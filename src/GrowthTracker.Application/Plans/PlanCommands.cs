using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Plans;

// ── CreatePlan ──────────────────────────────────────────────────────────────

/// <param name="GoalId">The Goal the plan is created from; it gets weight 1.</param>
/// <param name="AdditionalGoals">Other Goals the plan also contributes to (BR-01 allows many).</param>
public sealed record CreatePlanCommand(
    Guid GoalId,
    string Name,
    PlanType PlanType,
    decimal TargetValue,
    string? TargetUnit,
    DateOnly DurationStart,
    DateOnly DurationEnd,
    Difficulty Difficulty,
    WeekDays DaysOfWeek = WeekDays.None,
    int? FrequencyPerWeek = null,
    TimeOnly? PreferredTime = null,
    Guid? MilestoneId = null,
    IReadOnlyList<string>? Criteria = null,
    IReadOnlyList<(Guid GoalId, decimal Weight)>? AdditionalGoals = null) : ICommand<CreatePlanResult>;

public sealed record CreatePlanResult(Guid PlanId, int SessionsScheduled, DateOnly? FirstSession);

/// <summary>
/// Creates a Plan, schedules its first 8 weeks of sessions (FR-20) and moves Draft Goals to Active,
/// since a Goal becomes Active when its first Plan is scheduled.
/// </summary>
internal sealed class CreatePlanHandler(IAppDbContext db, IClock clock) : ICommandHandler<CreatePlanCommand, CreatePlanResult>
{
    public async Task<Result<CreatePlanResult>> HandleAsync(CreatePlanCommand c, CancellationToken ct)
    {
        var links = new List<(Guid GoalId, decimal Weight)> { (c.GoalId, 1m) };
        links.AddRange((c.AdditionalGoals ?? []).Where(g => g.GoalId != c.GoalId));
        var goalIds = links.Select(l => l.GoalId).ToList();

        var goals = await db.Goals.Where(g => goalIds.Contains(g.Id)).ToListAsync(ct);
        if (goals.Count != goalIds.Count)
            return Result<CreatePlanResult>.Failure(Errors.NotFound("goal"));
        if (goals.Any(g => g.IsClosed))
            return Result<CreatePlanResult>.Failure("goal.closed", "Plans cannot be added to a completed or cancelled goal.");

        if (c.MilestoneId is Guid milestoneId
            && !await db.Milestones.AnyAsync(m => m.Id == milestoneId && m.GoalId == c.GoalId, ct))
            return Result<CreatePlanResult>.Failure(Errors.NotFound("milestone"));

        var plan = new Plan(c.Name, c.PlanType, c.TargetValue, c.TargetUnit, c.DurationStart, c.DurationEnd,
                            c.Difficulty, links, c.DaysOfWeek, c.FrequencyPerWeek, c.PreferredTime,
                            c.MilestoneId, c.Criteria);
        db.Plans.Add(plan);

        var sessions = OccurrencePlanner.TopUp(plan, alreadyScheduled: new HashSet<DateOnly>(), clock.Today);
        db.PlanOccurrences.AddRange(sessions);
        if (sessions.Count > 0)
            foreach (var goal in goals)
                goal.Activate();

        await db.SaveChangesAsync(ct);
        return Result<CreatePlanResult>.Success(
            new CreatePlanResult(plan.Id, sessions.Count, sessions.Count > 0 ? sessions[0].ScheduledDate : null));
    }
}

// ── ArchivePlan ─────────────────────────────────────────────────────────────

public sealed record ArchivePlanCommand(Guid PlanId) : ICommand<Guid>;

internal sealed class ArchivePlanHandler(IAppDbContext db, IClock clock) : ICommandHandler<ArchivePlanCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ArchivePlanCommand command, CancellationToken ct)
    {
        if (!await db.Plans.AnyAsync(p => p.Id == command.PlanId, ct))
            return Result<Guid>.Failure(Errors.NotFound("plan"));

        await PlanArchiver.ArchiveAsync(db, [command.PlanId], clock.Today, ct);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(command.PlanId);
    }
}

// ── Shared helpers ──────────────────────────────────────────────────────────

/// <summary>Turns a Plan's schedule into new PlanOccurrence rows; also used by the weekly rollover job.</summary>
public static class OccurrencePlanner
{
    public static IReadOnlyList<PlanOccurrence> TopUp(Plan plan, IReadOnlySet<DateOnly> alreadyScheduled, DateOnly today)
    {
        if (plan.Status != PlanStatus.Active)
            return [];
        return ScheduleGenerator.Generate(plan.ToScheduleInput(), today, alreadyScheduled)
            .Select(date => new PlanOccurrence(plan.Id, date, plan.PreferredTime))
            .ToList();
    }
}

internal static class PlanArchiver
{
    /// <summary>
    /// Archives plans and soft-deletes their sessions from today on that have no result yet.
    /// Past sessions, logs and the XP ledger stay as history (BR-07).
    /// </summary>
    public static async Task ArchiveAsync(IAppDbContext db, IReadOnlyCollection<Guid> planIds, DateOnly today, CancellationToken ct)
    {
        if (planIds.Count == 0)
            return;

        var plans = await db.Plans.Where(p => planIds.Contains(p.Id)).ToListAsync(ct);
        foreach (var plan in plans)
            plan.Archive();

        var upcoming = await db.PlanOccurrences
            .Where(o => planIds.Contains(o.PlanId) && o.ScheduledDate >= today && o.State == OccurrenceState.Scheduled)
            .ToListAsync(ct);
        foreach (var occurrence in upcoming)
            occurrence.SoftDelete();
    }
}
