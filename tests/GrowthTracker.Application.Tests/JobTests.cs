using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Jobs;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Application.Settings;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Tests;

public class RolloverTests
{
    private static async Task<Guid> DailyPlanAsync(TestApp app, Guid goalId, int days, TimeOnly? time = null) =>
        (await app.Dispatcher.SendAsync(new CreatePlanCommand(goalId, "Daily", PlanType.Binary, 1, null,
            TestApp.Today, TestApp.Today.AddDays(days - 1), Difficulty.Easy, PreferredTime: time))).ShouldSucceed().PlanId;

    private static Task<List<PlanOccurrenceRow>> SessionsAsync(TestApp app, Guid planId) =>
        app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == planId)
            .Select(o => new PlanOccurrenceRow(o.ScheduledDate, o.State)).ToListAsync());

    private sealed record PlanOccurrenceRow(DateOnly Date, OccurrenceState State);

    [Fact]
    public async Task Rollover_MarksOverdueMissed_TopsUpEightWeeks_AndIsIdempotent()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var planId = await DailyPlanAsync(app, goal, days: 84);
        (await SessionsAsync(app, planId)).Should().HaveCount(56); // 8 weeks ahead at creation

        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(7);
        var result = (await app.Dispatcher.SendAsync(new RunRolloverCommand())).ShouldSucceed();

        result.Should().Be(new RolloverResult(MarkedMissed: 7, SessionsCreated: 7));
        var sessions = await SessionsAsync(app, planId);
        sessions.Should().HaveCount(63);
        sessions.Where(s => s.Date < TestApp.Today.AddDays(7)).Should().OnlyContain(s => s.State == OccurrenceState.Missed);
        sessions.Max(s => s.Date).Should().Be(TestApp.Today.AddDays(7 + 55));
        (await app.Dispatcher.QueryAsync(new GetAppSettingsQuery())).LastRolloverDate.Should().Be(TestApp.Today.AddDays(7));

        (await app.Dispatcher.SendAsync(new RunRolloverCommand())).ShouldSucceed().Should().Be(new RolloverResult(0, 0));
    }

    [Fact]
    public async Task Rollover_DoesNotRecreateMovedSessions_OrTopUpArchivedPlans()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var planId = await DailyPlanAsync(app, goal, days: 20);
        await app.WithDbAsync(async db =>
        {
            var session = await db.PlanOccurrences.SingleAsync(o => o.PlanId == planId && o.ScheduledDate == TestApp.Today.AddDays(3));
            session.Reschedule(TestApp.Today.AddDays(30), TestApp.Today, "Bận", app.Clock.UtcNow);
            return await db.SaveChangesAsync();
        });
        var archived = await DailyPlanAsync(app, goal, days: 90);
        await app.Dispatcher.SendAsync(new ArchivePlanCommand(archived));

        var result = (await app.Dispatcher.SendAsync(new RunRolloverCommand())).ShouldSucceed();

        result.SessionsCreated.Should().Be(0);
        (await SessionsAsync(app, planId)).Should().HaveCount(20);
    }

    [Fact]
    public async Task Rollover_MissedSessions_LowerGoalStatus()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(start: TestApp.Today, target: TestApp.Today.AddDays(20));
        await DailyPlanAsync(app, goal, days: 20);

        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(10);
        await app.Dispatcher.SendAsync(new RunRolloverCommand());

        (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!.Status.Should().Be(GoalStatus.AtRisk); // 0% vs 50%
    }
}

public class RiskScanAndReminderTests
{
    [Fact]
    public async Task RiskScan_UpdatesStatusWithoutAnyLog()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(start: TestApp.Today, target: TestApp.Today.AddDays(20));
        await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "Daily", PlanType.Binary, 1, null,
            TestApp.Today, TestApp.Today.AddDays(19), Difficulty.Easy));

        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(8);
        var atRisk = (await app.Dispatcher.SendAsync(new RunRiskScanCommand())).ShouldSucceed();

        atRisk.Should().Be(1);
        (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!.ExpectedProgress.Should().Be(40);
        (await app.Dispatcher.QueryAsync(new GetAppSettingsQuery())).LastRiskScanDate.Should().Be(TestApp.Today.AddDays(8));
    }

    [Fact]
    public async Task Reminders_FireTenMinutesBefore_OnlyForPendingSessions()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed(); // Mon/Wed/Fri 07:30
        var at = (int h, int m) => TestApp.Today.ToDateTime(new TimeOnly(h, m), DateTimeKind.Utc);

        var due = await app.Dispatcher.QueryAsync(new GetDueRemindersQuery(at(7, 15), at(7, 21)));
        due.Should().ContainSingle().Which.Should().Match<ReminderDto>(r =>
            r.PlanName == "Listening (Tech Pods)" && r.RemindAtUtc == at(7, 20) && r.Time == new TimeOnly(7, 30));
        (await app.Dispatcher.QueryAsync(new GetDueRemindersQuery(at(7, 20), at(7, 30)))).Should().BeEmpty(); // already sent

        await app.Dispatcher.SendAsync(new UpdateBackgroundSettingsCommand(RemindersEnabled: false));
        (await app.Dispatcher.QueryAsync(new GetDueRemindersQuery(at(7, 15), at(7, 21)))).Should().BeEmpty();

        await app.Dispatcher.SendAsync(new UpdateBackgroundSettingsCommand(RemindersEnabled: true));
        var session = await app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == plan.PlanId && o.ScheduledDate == TestApp.Today)
                                                                       .Select(o => o.Id).SingleAsync());
        await app.Dispatcher.SendAsync(new SkipOccurrenceCommand(session, SkipReason.Busy));
        (await app.Dispatcher.QueryAsync(new GetDueRemindersQuery(at(7, 15), at(7, 21)))).Should().BeEmpty();
    }
}

public class JobSchedulerTests
{
    [Fact]
    public async Task FirstTick_CatchesUpRolloverAndRiskScan_ThenOnlyOncePerDay()
    {
        await using var app = await TestApp.CreateAsync(); // clock: Monday 09:00
        var scheduler = app.Get<JobScheduler>();
        int changes = 0;
        scheduler.DataChanged += (_, _) => changes++;

        await scheduler.TickAsync();

        var settings = await app.Dispatcher.QueryAsync(new GetAppSettingsQuery());
        settings.LastRolloverDate.Should().Be(TestApp.Today);
        settings.LastRiskScanDate.Should().Be(TestApp.Today);
        changes.Should().Be(1);

        await scheduler.TickAsync();
        changes.Should().Be(1); // nothing more to do today
    }

    [Fact]
    public async Task RiskScan_WaitsUntilSixAm()
    {
        await using var app = await TestApp.CreateAsync();
        app.Clock.UtcNow = TestApp.Today.ToDateTime(new TimeOnly(5, 30), DateTimeKind.Utc);
        var scheduler = app.Get<JobScheduler>();

        await scheduler.TickAsync();
        (await app.Dispatcher.QueryAsync(new GetAppSettingsQuery())).LastRiskScanDate.Should().BeNull();

        app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(31);
        await scheduler.TickAsync();
        (await app.Dispatcher.QueryAsync(new GetAppSettingsQuery())).LastRiskScanDate.Should().Be(TestApp.Today);
    }

    [Fact]
    public async Task NextDay_RolloverRunsAfterFiveMinutesPastMidnight()
    {
        await using var app = await TestApp.CreateAsync();
        var scheduler = app.Get<JobScheduler>();
        await scheduler.TickAsync();

        app.Clock.UtcNow = TestApp.Today.AddDays(1).ToDateTime(new TimeOnly(0, 2), DateTimeKind.Utc);
        await scheduler.TickAsync();
        (await app.Dispatcher.QueryAsync(new GetAppSettingsQuery())).LastRolloverDate.Should().Be(TestApp.Today);

        app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(4);
        await scheduler.TickAsync();
        (await app.Dispatcher.QueryAsync(new GetAppSettingsQuery())).LastRolloverDate.Should().Be(TestApp.Today.AddDays(1));
    }

    [Fact]
    public async Task Reminder_IsSentOnce_AndNeverForThePastAtStartUp()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        await app.Dispatcher.SendAsync(Plans.Listening(goal)); // 07:30 today → remind at 07:20
        app.Clock.UtcNow = TestApp.Today.ToDateTime(new TimeOnly(7, 25), DateTimeKind.Utc);
        var scheduler = app.Get<JobScheduler>();

        await scheduler.TickAsync(); // start-up after 07:20: no stale reminder
        app.Notifier.Sent.Should().BeEmpty();

        // Wednesday: the scheduler is running across 07:20.
        app.Clock.UtcNow = TestApp.Today.AddDays(2).ToDateTime(new TimeOnly(7, 19, 50), DateTimeKind.Utc);
        await scheduler.TickAsync();
        app.Clock.UtcNow = app.Clock.UtcNow.AddSeconds(30);
        await scheduler.TickAsync();
        app.Clock.UtcNow = app.Clock.UtcNow.AddSeconds(30);
        await scheduler.TickAsync();

        app.Notifier.Sent.Should().ContainSingle().Which.Title.Should().Be("Sắp đến giờ: Listening (Tech Pods)");
        app.Notifier.Sent[0].Message.Should().StartWith("Buổi lúc 07:30.");
    }
}

public class BackgroundSettingsTests
{
    [Fact]
    public async Task Defaults_TrayOn_StartupOff_RemindersOn()
    {
        await using var app = await TestApp.CreateAsync();

        var settings = await app.Dispatcher.QueryAsync(new GetAppSettingsQuery());

        settings.Should().Be(new AppSettingsDto(CloseToTray: true, StartWithOs: false, RemindersEnabled: true,
                                                ReminderLeadMinutes: 10, null, null));
    }

    [Fact]
    public async Task StartWithOs_RegistersWithTheOs_AndPersists()
    {
        await using var app = await TestApp.CreateAsync();

        var settings = (await app.Dispatcher.SendAsync(new UpdateBackgroundSettingsCommand(StartWithOs: true, CloseToTray: false))).ShouldSucceed();

        settings.StartWithOs.Should().BeTrue();
        settings.CloseToTray.Should().BeFalse();
        app.Startup.IsRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task StartWithOs_RegistrationFailure_IsNotStored()
    {
        await using var app = await TestApp.CreateAsync();
        app.Startup.Fail = true;

        (await app.Dispatcher.SendAsync(new UpdateBackgroundSettingsCommand(StartWithOs: true))).ShouldFailWith("settings.startup");

        (await app.Dispatcher.QueryAsync(new GetAppSettingsQuery())).StartWithOs.Should().BeFalse();
    }
}
