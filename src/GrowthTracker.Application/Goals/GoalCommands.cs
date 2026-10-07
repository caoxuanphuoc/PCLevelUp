using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Plans;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Goals;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Goals;

/// <summary>Editable Goal fields (FR-02), shared by create and update.</summary>
public sealed record GoalFields(
    Guid AreaId,
    string Name,
    DateOnly StartDate,
    DateOnly TargetDate,
    int Priority,
    string? Description = null,
    string? TargetDescription = null);

// ── CreateGoal ──────────────────────────────────────────────────────────────

public sealed record CreateGoalCommand(GoalFields Fields) : ICommand<Guid>;

internal sealed class CreateGoalHandler(IAppDbContext db) : ICommandHandler<CreateGoalCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateGoalCommand command, CancellationToken ct)
    {
        var f = command.Fields;
        if (!await db.Areas.AnyAsync(a => a.Id == f.AreaId && !a.IsArchived, ct))
            return Result<Guid>.Failure(Errors.NotFound("area"));

        var goal = new Goal(f.AreaId, f.Name, f.StartDate, f.TargetDate, f.Priority, f.Description, f.TargetDescription);
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(goal.Id);
    }
}

// ── UpdateGoal ──────────────────────────────────────────────────────────────

public sealed record UpdateGoalCommand(Guid GoalId, GoalFields Fields) : ICommand<Guid>;

internal sealed class UpdateGoalHandler(IAppDbContext db) : ICommandHandler<UpdateGoalCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(UpdateGoalCommand command, CancellationToken ct)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == command.GoalId, ct);
        if (goal is null)
            return Result<Guid>.Failure(Errors.NotFound("goal"));
        var f = command.Fields;
        if (f.AreaId != goal.AreaId && !await db.Areas.AnyAsync(a => a.Id == f.AreaId && !a.IsArchived, ct))
            return Result<Guid>.Failure(Errors.NotFound("area"));

        goal.Update(f.AreaId, f.Name, f.StartDate, f.TargetDate, f.Priority, f.Description, f.TargetDescription);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(goal.Id);
    }
}

// ── CancelGoal ──────────────────────────────────────────────────────────────

/// <summary>
/// Stops pursuing a Goal. Plans that contribute only to this Goal are archived and lose their future sessions;
/// plans shared with other Goals keep running. Earned XP is untouched (BR-07).
/// </summary>
public sealed record CancelGoalCommand(Guid GoalId) : ICommand<Guid>;

internal sealed class CancelGoalHandler(IAppDbContext db, IClock clock) : ICommandHandler<CancelGoalCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CancelGoalCommand command, CancellationToken ct)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == command.GoalId, ct);
        if (goal is null)
            return Result<Guid>.Failure(Errors.NotFound("goal"));

        goal.Cancel();

        var exclusivePlanIds = await db.PlanGoalLinks
            .Where(l => l.GoalId == goal.Id)
            .Select(l => l.PlanId)
            .Where(planId => !db.PlanGoalLinks.Any(other => other.PlanId == planId && other.GoalId != goal.Id))
            .ToListAsync(ct);
        await PlanArchiver.ArchiveAsync(db, exclusivePlanIds, clock.Today, ct);

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(goal.Id);
    }
}
