using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.PlanLogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Logging;

// ── LogPlanResult ───────────────────────────────────────────────────────────

/// <summary>Records the result of a session (FR-31): Score, XP, progress and status are computed, never entered (BR-03).</summary>
public sealed record LogPlanResultCommand(Guid OccurrenceId, LogEntry Entry) : ICommand<LogResultDto>;

internal sealed class LogPlanResultHandler(IAppDbContext db, IClock clock, ScoreEngine engine, LogEffects effects,
                                           LogResultBuilder results) : ICommandHandler<LogPlanResultCommand, LogResultDto>
{
    public async Task<Result<LogResultDto>> HandleAsync(LogPlanResultCommand command, CancellationToken ct)
    {
        var occurrence = await db.PlanOccurrences.FirstOrDefaultAsync(o => o.Id == command.OccurrenceId, ct);
        if (occurrence is null)
            return Result<LogResultDto>.Failure(Errors.NotFound("occurrence"));
        if (await db.PlanLogs.AnyAsync(l => l.OccurrenceId == occurrence.Id, ct))
            return Result<LogResultDto>.Failure("planlog.already_logged", "This session already has a result.");
        var plan = await db.Plans.Include(p => p.Criteria).Include(p => p.GoalLinks).FirstAsync(p => p.Id == occurrence.PlanId, ct);

        await using var transaction = await db.BeginTransactionAsync(ct);
        var now = clock.UtcNow;
        ApplyCriteria(plan, command.Entry, now);
        var breakdown = engine.Calculate(await ScoreInputs.BuildAsync(db, plan, occurrence, command.Entry, ct));
        var log = PlanLog.Record(occurrence, ActualFor(plan, command.Entry), command.Entry.Quality,
                                 command.Entry.Note, breakdown, clock.Today, now,
                                 countsAsDone: ScoreInputs.CountsAsDone(plan.PlanType, command.Entry, plan));
        db.PlanLogs.Add(log);
        await effects.CreditXpAsync(plan, log.Id, log.Xp, "PlanLogged", ct);
        await db.SaveChangesAsync(ct);

        await effects.RecomputeGoalsAsync(plan, clock.Today, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<LogResultDto>.Success((await results.BuildAsync(log.Id, ct))!);
    }

    internal static void ApplyCriteria(Plan plan, LogEntry entry, DateTime now)
    {
        if (plan.PlanType != PlanType.Achievement || entry.CriteriaDoneIds is null)
            return;
        foreach (var criterion in plan.Criteria)
        {
            bool done = entry.CriteriaDoneIds.Contains(criterion.Id);
            if (done != criterion.IsDone)
                criterion.SetDone(done, now);
        }
    }

    /// <summary>The value stored as ActualValue: minutes/units, 1/0 for done/not done, or criteria done.</summary>
    internal static decimal ActualFor(Plan plan, LogEntry entry) => plan.PlanType switch
    {
        PlanType.Time or PlanType.Quantity => Math.Max(0, entry.Actual),
        PlanType.Achievement => plan.Criteria.Count(c => c.IsDone),
        _ => entry.Done ? 1 : 0,
    };
}

// ── EditPlanLog ─────────────────────────────────────────────────────────────

/// <summary>
/// Changes a result within 24 hours (BR-11, FR-33). The Score is recomputed and the XP difference is appended
/// to the ledger, which is never rewritten (BR-07).
/// </summary>
public sealed record EditPlanLogCommand(Guid PlanLogId, LogEntry Entry) : ICommand<LogResultDto>;

internal sealed class EditPlanLogHandler(IAppDbContext db, IClock clock, ScoreEngine engine, LogEffects effects,
                                         LogResultBuilder results) : ICommandHandler<EditPlanLogCommand, LogResultDto>
{
    public async Task<Result<LogResultDto>> HandleAsync(EditPlanLogCommand command, CancellationToken ct)
    {
        var log = await db.PlanLogs.FirstOrDefaultAsync(l => l.Id == command.PlanLogId, ct);
        if (log is null)
            return Result<LogResultDto>.Failure(Errors.NotFound("planlog"));
        var now = clock.UtcNow;
        if (!log.CanEdit(now))
            return Result<LogResultDto>.Failure("planlog.edit_window", "Plan logs can only be edited within 24 hours (BR-11).");

        var occurrence = await db.PlanOccurrences.FirstAsync(o => o.Id == log.OccurrenceId, ct);
        var plan = await db.Plans.Include(p => p.Criteria).Include(p => p.GoalLinks).FirstAsync(p => p.Id == occurrence.PlanId, ct);

        await using var transaction = await db.BeginTransactionAsync(ct);
        decimal previousXp = log.Xp;
        LogPlanResultHandler.ApplyCriteria(plan, command.Entry, now);
        var breakdown = engine.Calculate(await ScoreInputs.BuildAsync(db, plan, occurrence, command.Entry, ct));
        log.Edit(LogPlanResultHandler.ActualFor(plan, command.Entry), command.Entry.Quality, command.Entry.Note, breakdown, now,
                 occurrence, ScoreInputs.CountsAsDone(plan.PlanType, command.Entry, plan));
        await effects.CreditXpAsync(plan, log.Id, log.Xp - previousXp, "PlanLogEdited", ct);
        await db.SaveChangesAsync(ct);

        await effects.RecomputeGoalsAsync(plan, clock.Today, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<LogResultDto>.Success((await results.BuildAsync(log.Id, ct))!);
    }
}

// ── SkipOccurrence ──────────────────────────────────────────────────────────

/// <summary>Skips a session with a reason (FR-32). It gets no Score but still counts against Consistency (BR-10).</summary>
public sealed record SkipOccurrenceCommand(Guid OccurrenceId, SkipReason Reason) : ICommand<Guid>;

internal sealed class SkipOccurrenceHandler(IAppDbContext db, IClock clock, LogEffects effects)
    : ICommandHandler<SkipOccurrenceCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(SkipOccurrenceCommand command, CancellationToken ct)
    {
        var occurrence = await db.PlanOccurrences.FirstOrDefaultAsync(o => o.Id == command.OccurrenceId, ct);
        if (occurrence is null)
            return Result<Guid>.Failure(Errors.NotFound("occurrence"));
        if (await db.PlanLogs.AnyAsync(l => l.OccurrenceId == occurrence.Id, ct))
            return Result<Guid>.Failure("occurrence.already_done", "A session with a result cannot be skipped.");

        occurrence.Skip(command.Reason, clock.Today);
        await db.SaveChangesAsync(ct);

        var plan = await db.Plans.Include(p => p.GoalLinks).FirstAsync(p => p.Id == occurrence.PlanId, ct);
        await effects.RecomputeGoalsAsync(plan, clock.Today, ct);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(occurrence.Id);
    }
}

// ── GetLogResult ────────────────────────────────────────────────────────────

public sealed record GetLogResultQuery(Guid PlanLogId) : IQuery<LogResultDto?>;

internal sealed class GetLogResultHandler(LogResultBuilder results) : IQueryHandler<GetLogResultQuery, LogResultDto?>
{
    public Task<LogResultDto?> HandleAsync(GetLogResultQuery query, CancellationToken ct) => results.BuildAsync(query.PlanLogId, ct);
}
