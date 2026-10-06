using GrowthTracker.Domain.Areas;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.PlanLogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scoring;
using GrowthTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowthTracker.Infrastructure.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task Initialize_AppliesMigrations_AndSeedsSixAreas()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.NewContext();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        var areas = await db.Areas.OrderBy(a => a.SortOrder).ToListAsync();
        areas.Select(a => a.Name).Should().Equal(Area.Defaults.Select(d => d.Name));
        areas.Should().OnlyContain(a => a.CreatedAt == database.Clock.UtcNow);
    }

    [Fact]
    public async Task Initialize_IsIdempotent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var db = database.NewContext())
            await new DatabaseInitializer(db, new NoBackup(), NullLogger<DatabaseInitializer>.Instance).InitializeAsync();

        await using var check = database.NewContext();
        (await check.Areas.CountAsync()).Should().Be(6);
    }

    [Fact]
    public async Task GoalPlanOccurrenceLog_RoundTrip()
    {
        await using var database = await TestDatabase.CreateAsync();
        var today = new DateOnly(2026, 10, 6);
        Guid planId, logId;

        await using (var db = database.NewContext())
        {
            var area = await db.Areas.FirstAsync(a => a.Name == "English");
            var goal = new Goal(area.Id, "Improve English", today, today.AddMonths(6), priority: 2);
            var plan = new Plan("Speaking", PlanType.Time, 20, "min", today, today.AddMonths(3), Difficulty.Hard,
                                [(goal.Id, 1m)], WeekDays.Weekdays, preferredTime: new TimeOnly(20, 0));
            var occurrence = new PlanOccurrence(plan.Id, today, plan.PreferredTime);
            var breakdown = new ScoreEngine().Calculate(new ScoreInput(PlanType.Time, Difficulty.Hard, 4, 5, 5, 18, 20));
            var log = PlanLog.Record(occurrence, 18, 4, "note", breakdown, today, database.Clock.UtcNow);

            db.AddRange(goal, plan, occurrence, log, new XpLedgerEntry(log.Id, area.Id, log.Xp, "PlanLogged"));
            await db.SaveChangesAsync();
            planId = plan.Id;
            logId = log.Id;
        }

        await using (var db = database.NewContext())
        {
            var plan = await db.Plans.Include(p => p.GoalLinks).SingleAsync(p => p.Id == planId);
            plan.GoalLinks.Should().ContainSingle();
            plan.DaysOfWeek.Should().Be(WeekDays.Weekdays);
            plan.PreferredTime.Should().Be(new TimeOnly(20, 0));

            var log = await db.PlanLogs.SingleAsync(l => l.Id == logId);
            log.Score.Should().BeInRange(0, 100);
            log.ScoreVersion.Should().Be(ScoreWeights.Default.Version);

            var occurrence = await db.PlanOccurrences.SingleAsync(o => o.Id == log.OccurrenceId);
            occurrence.State.Should().Be(OccurrenceState.Done);

            (await db.XpLedger.SumAsync(x => x.Amount)).Should().Be(log.Xp);
        }
    }

    [Fact]
    public async Task ForeignKeys_AreEnforced()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.NewContext();

        db.Goals.Add(new Goal(Guid.NewGuid(), "Orphan", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), 1));

        await ((Func<Task>)(() => db.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SoftDeleted_AreFilteredOut_AndUpdatedAtIsStamped()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var db = database.NewContext())
        {
            var area = await db.Areas.FirstAsync(a => a.Name == "Finance");
            database.Clock.UtcNow = database.Clock.UtcNow.AddHours(1);
            area.SoftDelete();
            await db.SaveChangesAsync();
            area.UpdatedAt.Should().Be(database.Clock.UtcNow);
        }

        await using var check = database.NewContext();
        (await check.Areas.CountAsync()).Should().Be(5);
        (await check.Areas.IgnoreQueryFilters().CountAsync()).Should().Be(6);
    }
}
