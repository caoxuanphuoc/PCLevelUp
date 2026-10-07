using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Areas;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Goals;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Areas;

public sealed record AreaDto(Guid Id, string Name, string ColorHex, int SortOrder, bool IsArchived, int ActiveGoalCount);

// ── GetAreas ────────────────────────────────────────────────────────────────

public sealed record GetAreasQuery(bool IncludeArchived = false) : IQuery<IReadOnlyList<AreaDto>>;

internal sealed class GetAreasHandler(IAppDbContext db) : IQueryHandler<GetAreasQuery, IReadOnlyList<AreaDto>>
{
    public async Task<IReadOnlyList<AreaDto>> HandleAsync(GetAreasQuery query, CancellationToken ct)
    {
        var goalCounts = await db.Goals
            .Where(g => g.Status != GoalStatus.Cancelled && g.Status != GoalStatus.Completed)
            .GroupBy(g => g.AreaId)
            .Select(g => new { AreaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AreaId, x => x.Count, ct);

        var areas = await db.Areas
            .Where(a => query.IncludeArchived || !a.IsArchived)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .ToListAsync(ct);

        return areas.Select(a => new AreaDto(a.Id, a.Name, a.ColorHex, a.SortOrder, a.IsArchived,
                                             goalCounts.GetValueOrDefault(a.Id))).ToList();
    }
}

// ── CreateArea ──────────────────────────────────────────────────────────────

public sealed record CreateAreaCommand(string Name, string ColorHex) : ICommand<Guid>;

internal sealed class CreateAreaHandler(IAppDbContext db) : ICommandHandler<CreateAreaCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateAreaCommand command, CancellationToken ct)
    {
        int nextOrder = await db.Areas.IgnoreQueryFilters().Select(a => (int?)a.SortOrder).MaxAsync(ct) ?? -1;
        var area = new Area(command.Name, command.ColorHex, nextOrder + 1);
        db.Areas.Add(area);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(area.Id);
    }
}

// ── UpdateArea ──────────────────────────────────────────────────────────────

public sealed record UpdateAreaCommand(Guid AreaId, string Name, string ColorHex) : ICommand<Guid>;

internal sealed class UpdateAreaHandler(IAppDbContext db) : ICommandHandler<UpdateAreaCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(UpdateAreaCommand command, CancellationToken ct)
    {
        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == command.AreaId, ct);
        if (area is null)
            return Result<Guid>.Failure(Errors.NotFound("area"));

        area.Rename(command.Name);
        area.Recolor(command.ColorHex);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(area.Id);
    }
}

// ── ArchiveArea ─────────────────────────────────────────────────────────────

/// <summary>Archive or restore an Area (FR-01). Goals keep their Area; archived Areas are hidden from pickers.</summary>
public sealed record SetAreaArchivedCommand(Guid AreaId, bool Archived) : ICommand<Guid>;

internal sealed class SetAreaArchivedHandler(IAppDbContext db) : ICommandHandler<SetAreaArchivedCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(SetAreaArchivedCommand command, CancellationToken ct)
    {
        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == command.AreaId, ct);
        if (area is null)
            return Result<Guid>.Failure(Errors.NotFound("area"));

        if (command.Archived)
        {
            if (await db.Areas.CountAsync(a => !a.IsArchived, ct) <= 1)
                return Result<Guid>.Failure("area.last_active", "At least one area must stay active.");
            area.Archive();
        }
        else
        {
            area.Restore();
        }
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(area.Id);
    }
}
