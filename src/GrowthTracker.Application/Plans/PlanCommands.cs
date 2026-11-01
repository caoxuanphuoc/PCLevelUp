using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Logging;
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

// ── UpdatePlan ──────────────────────────────────────────────────────────────

/// <summary>
/// Edits a plan: name, target, days, preferred time, difficulty, end date and milestone. Type, start date and
/// criteria stay fixed. Only sessions after today are rescheduled (BR-14); logs, Score and XP stay as they are.
/// </summary>
public sealed record UpdatePlanCommand(
    Guid PlanId,
    string Name,
    decimal TargetValue,
    string? TargetUnit,
    DateOnly DurationEnd,
    Difficulty Difficulty,
    WeekDays DaysOfWeek = WeekDays.None,
    int? FrequencyPerWeek = null,
    TimeOnly? PreferredTime = null,
    Guid? MilestoneId = null) : ICommand<UpdatePlanResult>;

public sealed record UpdatePlanResult(Guid PlanId, int SessionsAdded, int SessionsRemoved);

internal sealed class UpdatePlanHandler(IAppDbContext db, IClock clock, LogEffects effects)
    : ICommandHandler<UpdatePlanCommand, UpdatePlanResult>
{
    public async Task<Result<UpdatePlanResult>> HandleAsync(UpdatePlanCommand c, CancellationToken ct)
    {
        var plan = await db.Plans.Include(p => p.GoalLinks).Include(p => p.Criteria).FirstOrDefaultAsync(p => p.Id == c.PlanId, ct);
        if (plan is null)
            return Result<UpdatePlanResult>.Failure(Errors.NotFound("plan"));
        var goalIds = plan.GoalLinks.Select(l => l.GoalId).ToList();
        if (c.MilestoneId is Guid milestoneId
            && !await db.Milestones.AnyAsync(m => m.Id == milestoneId && goalIds.Contains(m.GoalId), ct))
            return Result<UpdatePlanResult>.Failure(Errors.NotFound("milestone"));

        var today = clock.Today;
        var daysBefore = ScheduleGenerator.EffectiveDays(plan.ToScheduleInput());
        var endBefore = plan.DurationEnd;
        var timeBefore = plan.PreferredTime;
        plan.Edit(c.Name, c.TargetValue, c.TargetUnit, c.DaysOfWeek, c.FrequencyPerWeek, c.PreferredTime,
                  c.Difficulty, c.DurationEnd, today);
        plan.MoveToMilestone(c.MilestoneId);

        await using var transaction = await db.BeginTransactionAsync(ct);
        int added = 0, removed = 0;
        if (ScheduleGenerator.EffectiveDays(plan.ToScheduleInput()) != daysBefore)
            // A plan that had already ended may get a session today; a running plan keeps today's as it is.
            (added, removed) = await PlanRescheduler.RespreadAsync(db, plan, today, keepToday: endBefore >= today, ct);
        else if (plan.DurationEnd > endBefore)
            added = await PlanRescheduler.ExtendAsync(db, plan, today, ct);
        else if (plan.DurationEnd < endBefore)
            removed = await PlanRescheduler.TrimAfterEndAsync(db, plan, ct);
        if (plan.PreferredTime != timeBefore)
            await PlanRescheduler.FollowPreferredTimeAsync(db, plan, timeBefore, today, ct);
        await db.SaveChangesAsync(ct);

        // The end date, days and milestone all change Plan → Milestone → Goal progress.
        await effects.RecomputeGoalsAsync(plan, today, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<UpdatePlanResult>.Success(new UpdatePlanResult(plan.Id, added, removed));
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

/// <summary>Brings a plan's sessions in line after it changes; sessions up to today and their results stay as they are (BR-14).</summary>
internal static class PlanRescheduler
{
    /// <summary>
    /// New days or sessions per week: drops the scheduled sessions after today (including ones moved by hand) and
    /// schedules them again from the new pattern. Returns how many sessions were added and removed.
    /// </summary>
    public static async Task<(int Added, int Removed)> RespreadAsync(IAppDbContext db, Plan plan, DateOnly today, bool keepToday,
                                                                     CancellationToken ct)
    {
        var future = await db.PlanOccurrences.Where(o => o.PlanId == plan.Id && o.State == OccurrenceState.Scheduled && o.ScheduledDate > today)
                                             .ToListAsync(ct);
        foreach (var session in future)
            session.SoftDelete();
        var removedIds = future.Select(f => f.Id).ToHashSet();
        var taken = (await db.PlanOccurrences.Where(o => o.PlanId == plan.Id && o.ScheduledDate >= today)
                                             .Select(o => new { o.Id, o.ScheduledDate }).ToListAsync(ct))
                    .Where(o => !removedIds.Contains(o.Id))
                    .Select(o => o.ScheduledDate)
                    .ToHashSet();
        if (keepToday)
            taken.Add(today);
        var added = OccurrencePlanner.TopUp(plan, taken, today);
        db.PlanOccurrences.AddRange(added);
        return (added.Count, future.Count);
    }

    /// <summary>A later end date: schedules the new days (up to the rolling horizon; the rollover job does the rest).</summary>
    public static async Task<int> ExtendAsync(IAppDbContext db, Plan plan, DateOnly today, CancellationToken ct)
    {
        // A moved session still "owns" its original date, as in the rollover job.
        var taken = (await db.PlanOccurrences.Where(o => o.PlanId == plan.Id && (o.ScheduledDate >= today || o.OriginalDate >= today))
                                             .Select(o => new { o.ScheduledDate, o.OriginalDate }).ToListAsync(ct))
                    .SelectMany(o => new[] { o.ScheduledDate, o.OriginalDate })
                    .ToHashSet();
        var added = OccurrencePlanner.TopUp(plan, taken, today);
        db.PlanOccurrences.AddRange(added);
        return added.Count;
    }

    /// <summary>An earlier end date: removes the scheduled sessions after it.</summary>
    public static async Task<int> TrimAfterEndAsync(IAppDbContext db, Plan plan, CancellationToken ct)
    {
        var after = await db.PlanOccurrences.Where(o => o.PlanId == plan.Id && o.State == OccurrenceState.Scheduled && o.ScheduledDate > plan.DurationEnd)
                                            .ToListAsync(ct);
        foreach (var session in after)
            session.SoftDelete();
        return after.Count;
    }

    /// <summary>A new preferred time moves the time of upcoming sessions that still used the old one (hand-set times stay).</summary>
    public static async Task FollowPreferredTimeAsync(IAppDbContext db, Plan plan, TimeOnly? previous, DateOnly today, CancellationToken ct)
    {
        var upcoming = await db.PlanOccurrences.Where(o => o.PlanId == plan.Id && o.State == OccurrenceState.Scheduled
                                                           && o.ScheduledDate >= today && o.ScheduledTime == previous)
                                               .ToListAsync(ct);
        foreach (var session in upcoming)
            session.ChangeTime(plan.PreferredTime);
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
