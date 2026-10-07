using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Application.Settings;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Jobs;

// ── RunRollover ─────────────────────────────────────────────────────────────

public sealed record RolloverResult(int MarkedMissed, int SessionsCreated);

/// <summary>
/// Daily rollover (TDD §7.1): past sessions without a result become Missed, and every active plan is topped up
/// so its next 8 weeks are scheduled. Idempotent, so it is safe to run at every start-up as catch-up.
/// </summary>
public sealed record RunRolloverCommand : ICommand<RolloverResult>;

internal sealed class RunRolloverHandler(IAppDbContext db, IClock clock, LogEffects effects)
    : ICommandHandler<RunRolloverCommand, RolloverResult>
{
    public async Task<Result<RolloverResult>> HandleAsync(RunRolloverCommand command, CancellationToken ct)
    {
        var today = clock.Today;

        var overdue = await db.PlanOccurrences
            .Where(o => o.State == OccurrenceState.Scheduled && o.ScheduledDate < today)
            .ToListAsync(ct);
        foreach (var occurrence in overdue)
            occurrence.MarkMissedIfOverdue(today);

        var plans = await db.Plans.Include(p => p.GoalLinks)
            .Where(p => p.Status == PlanStatus.Active && p.DurationEnd >= today)
            .ToListAsync(ct);
        var planIds = plans.Select(p => p.Id).ToList();
        // A moved session still "owns" its original date, so topping up must not recreate it there.
        var taken = (await db.PlanOccurrences.Where(o => planIds.Contains(o.PlanId) && (o.ScheduledDate >= today || o.OriginalDate >= today))
                                             .Select(o => new { o.PlanId, o.ScheduledDate, o.OriginalDate })
                                             .ToListAsync(ct))
            .GroupBy(o => o.PlanId)
            .ToDictionary(g => g.Key, g => g.SelectMany(o => new[] { o.ScheduledDate, o.OriginalDate }).ToHashSet());

        int created = 0;
        foreach (var plan in plans)
        {
            var sessions = OccurrencePlanner.TopUp(plan, taken.GetValueOrDefault(plan.Id) ?? [], today);
            db.PlanOccurrences.AddRange(sessions);
            created += sessions.Count;
        }

        await SettingKeys.SetDateAsync(db, SettingKeys.LastRolloverDate, today, ct);
        await db.SaveChangesAsync(ct);

        if (overdue.Count > 0)
        {
            var affectedPlans = overdue.Select(o => o.PlanId).ToHashSet();
            var goalIds = await db.PlanGoalLinks.Where(l => affectedPlans.Contains(l.PlanId)).Select(l => l.GoalId).ToListAsync(ct);
            await effects.RecomputeGoalsAsync(goalIds, today, ct);
            await db.SaveChangesAsync(ct);
        }
        return Result<RolloverResult>.Success(new RolloverResult(overdue.Count, created));
    }
}

// ── RunRiskScan ─────────────────────────────────────────────────────────────

/// <summary>
/// Daily risk scan (TDD §7.1, FR-50): recomputes progress, expected progress and On Track / At Risk for every
/// open Goal, so status follows the calendar even on days nothing is logged. Returns how many Goals are At Risk.
/// </summary>
public sealed record RunRiskScanCommand : ICommand<int>;

internal sealed class RunRiskScanHandler(IAppDbContext db, IClock clock, LogEffects effects) : ICommandHandler<RunRiskScanCommand, int>
{
    public async Task<Result<int>> HandleAsync(RunRiskScanCommand command, CancellationToken ct)
    {
        var today = clock.Today;
        var goalIds = await db.Goals.Where(g => g.Status != GoalStatus.Completed && g.Status != GoalStatus.Cancelled)
                                    .Select(g => g.Id).ToListAsync(ct);
        await effects.RecomputeGoalsAsync(goalIds, today, ct);
        await SettingKeys.SetDateAsync(db, SettingKeys.LastRiskScanDate, today, ct);
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(await db.Goals.CountAsync(g => g.Status == GoalStatus.AtRisk, ct));
    }
}

// ── GetDueReminders ─────────────────────────────────────────────────────────

public sealed record ReminderDto(Guid OccurrenceId, string PlanName, DateOnly Date, TimeOnly Time, DateTime RemindAtUtc);

/// <summary>
/// Sessions whose reminder time — the preferred time minus the lead (10 minutes) — falls in (FromUtc, ToUtc].
/// Only sessions still waiting for a result are included.
/// </summary>
public sealed record GetDueRemindersQuery(DateTime FromUtc, DateTime ToUtc) : IQuery<IReadOnlyList<ReminderDto>>;

internal sealed class GetDueRemindersHandler(IAppDbContext db, IClock clock) : IQueryHandler<GetDueRemindersQuery, IReadOnlyList<ReminderDto>>
{
    public async Task<IReadOnlyList<ReminderDto>> HandleAsync(GetDueRemindersQuery query, CancellationToken ct)
    {
        var settings = SettingKeys.ToDto(await SettingKeys.LoadAsync(db, ct));
        if (!settings.RemindersEnabled || query.ToUtc <= query.FromUtc)
            return [];

        var lead = TimeSpan.FromMinutes(settings.ReminderLeadMinutes);
        var tz = clock.TimeZone;
        // A reminder can belong to the next local day when the lead crosses midnight.
        var firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Utc(query.FromUtc), tz));
        var lastDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Utc(query.ToUtc) + lead, tz));

        var sessions = await (
                from o in db.PlanOccurrences
                join p in db.Plans on o.PlanId equals p.Id
                where o.State == OccurrenceState.Scheduled && o.ScheduledTime != null
                      && o.ScheduledDate >= firstDay && o.ScheduledDate <= lastDay
                      && p.Status == PlanStatus.Active
                select new { o.Id, p.Name, o.ScheduledDate, o.ScheduledTime })
            .ToListAsync(ct);

        return sessions
            .Select(s =>
            {
                var local = s.ScheduledDate.ToDateTime(s.ScheduledTime!.Value, DateTimeKind.Unspecified);
                var remindAt = TimeZoneInfo.ConvertTimeToUtc(local, tz) - lead;
                return new ReminderDto(s.Id, s.Name, s.ScheduledDate, s.ScheduledTime.Value, remindAt);
            })
            .Where(r => r.RemindAtUtc > Utc(query.FromUtc) && r.RemindAtUtc <= Utc(query.ToUtc))
            .OrderBy(r => r.RemindAtUtc)
            .ToList();
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
