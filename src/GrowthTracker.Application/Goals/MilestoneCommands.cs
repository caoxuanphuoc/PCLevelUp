using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Milestones;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Goals;

// ── AddMilestone ────────────────────────────────────────────────────────────

/// <summary>Adds a Milestone at the end of the Goal's list; weights default to equal (FR-03).</summary>
public sealed record AddMilestoneCommand(Guid GoalId, string Name, DateOnly? DueDate = null, decimal Weight = 1m)
    : ICommand<Guid>;

internal sealed class AddMilestoneHandler(IAppDbContext db) : ICommandHandler<AddMilestoneCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(AddMilestoneCommand command, CancellationToken ct)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == command.GoalId, ct);
        if (goal is null)
            return Result<Guid>.Failure(Errors.NotFound("goal"));
        if (goal.IsClosed)
            return Result<Guid>.Failure("goal.closed", "Milestones cannot be added to a completed or cancelled goal.");

        int nextOrder = await db.Milestones.Where(m => m.GoalId == goal.Id)
                                           .Select(m => (int?)m.SortOrder).MaxAsync(ct) ?? -1;
        var milestone = new Milestone(goal.Id, command.Name, nextOrder + 1, command.Weight, command.DueDate);
        db.Milestones.Add(milestone);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(milestone.Id);
    }
}

// ── RemoveMilestone ─────────────────────────────────────────────────────────

/// <summary>Soft-deletes a Milestone. Its Plans keep running, attached directly to the Goal.</summary>
public sealed record RemoveMilestoneCommand(Guid MilestoneId) : ICommand<Guid>;

internal sealed class RemoveMilestoneHandler(IAppDbContext db) : ICommandHandler<RemoveMilestoneCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RemoveMilestoneCommand command, CancellationToken ct)
    {
        var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == command.MilestoneId, ct);
        if (milestone is null)
            return Result<Guid>.Failure(Errors.NotFound("milestone"));

        var plans = await db.Plans.Where(p => p.MilestoneId == milestone.Id).ToListAsync(ct);
        foreach (var plan in plans)
            plan.MoveToMilestone(null);
        milestone.SoftDelete();

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(milestone.Id);
    }
}
