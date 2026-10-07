using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Tests;

public class LoggingUseCaseTests
{
    private static async Task<Guid> SessionOnAsync(TestApp app, Guid planId, DateOnly date) =>
        await app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == planId && o.ScheduledDate == date).Select(o => o.Id).SingleAsync());

    private static async Task<Guid> DailyPlanAsync(TestApp app, Guid goalId, int days = 10, Difficulty difficulty = Difficulty.Easy) =>
        (await app.Dispatcher.SendAsync(new CreatePlanCommand(goalId, "Meditate", PlanType.Binary, 1, null,
            TestApp.Today, TestApp.Today.AddDays(days - 1), difficulty))).ShouldSucceed().PlanId;

    private static void AdvanceDays(TestApp app, int days) => app.Clock.UtcNow = app.Clock.UtcNow.AddDays(days);

    [Fact]
    public async Task LogTimePlan_ScoresWithV2_CreditsXp_AndShowsOnToday()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        var session = await SessionOnAsync(app, plan.PlanId, TestApp.Today);

        var result = (await app.Dispatcher.SendAsync(new LogPlanResultCommand(session, new LogEntry(25, true, 4, "Tập trung tốt")))).ShouldSucceed();

        // C = 100 (25/20 capped, BR-09), Q = 80, S = 100 (1/1) → 45 + 24 + 25 = 94; Normal ×1.05 → 98.7 XP.
        result.Completion.Should().Be(100);
        result.Quality.Should().Be(80);
        result.Consistency.Should().Be(100);
        result.Score.Should().Be(94);
        result.Xp.Should().Be(98.7m);
        result.Weights.Should().Be(ScoreWeights.V2);
        result.AreaName.Should().Be("English");
        result.AreaLevel.TotalXp.Should().Be(98.7m);
        result.CurrentStreak.Should().Be(1);
        result.CanEdit.Should().BeTrue();

        var today = await app.Dispatcher.QueryAsync(new GetTodayQuery());
        var row = today.Sessions.Should().ContainSingle().Which;
        row.State.Should().Be(OccurrenceState.Done);
        row.Log!.Score.Should().Be(94);
        today.XpToday.Should().Be(98.7m);
        today.DoneCount.Should().Be(1);
        today.Streak.Current.Should().Be(1);
        today.Areas.Single(a => a.Name == "English").Level.TotalXp.Should().Be(98.7m);
        today.Week.Single(d => d.Date == TestApp.Today).AverageScore.Should().Be(94);
    }

    [Fact]
    public async Task LogTwice_Fails_AndFutureSession_CannotBeLogged()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        var monday = await SessionOnAsync(app, plan.PlanId, TestApp.Today);
        var wednesday = await SessionOnAsync(app, plan.PlanId, TestApp.Today.AddDays(2));

        (await app.Dispatcher.SendAsync(new LogPlanResultCommand(monday, new LogEntry(20, true, 3, null)))).ShouldSucceed();
        (await app.Dispatcher.SendAsync(new LogPlanResultCommand(monday, new LogEntry(20, true, 3, null)))).ShouldFailWith("planlog.already_logged");
        (await app.Dispatcher.SendAsync(new LogPlanResultCommand(wednesday, new LogEntry(20, true, 3, null)))).ShouldFailWith("planlog.future_date");
        (await app.Dispatcher.QueryAsync(new GetPlanResultFormQuery(wednesday)))!.CanLog.Should().BeFalse();
        (await app.Dispatcher.SendAsync(new LogPlanResultCommand(Guid.NewGuid(), new LogEntry(1, true, 3, null)))).ShouldFailWith("occurrence.not_found");
    }

    [Fact]
    public async Task Edit_Within24h_RecomputesScore_AndAppendsXpDifference()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        var session = await SessionOnAsync(app, plan.PlanId, TestApp.Today);
        var logged = (await app.Dispatcher.SendAsync(new LogPlanResultCommand(session, new LogEntry(25, true, 4, null)))).ShouldSucceed();

        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(23);
        var edited = (await app.Dispatcher.SendAsync(new EditPlanLogCommand(logged.PlanLogId, new LogEntry(25, true, 5, "sửa")))).ShouldSucceed();

        edited.Score.Should().Be(100);
        edited.Xp.Should().Be(105m);
        var ledger = await app.WithDbAsync(db => db.XpLedger.OrderBy(x => x.CreatedAt).Select(x => new { x.Amount, x.Reason }).ToListAsync());
        ledger.Select(x => x.Reason).Should().Equal("PlanLogged", "PlanLogEdited"); // append-only (BR-07)
        ledger.Sum(x => x.Amount).Should().Be(105m);

        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); // 25h after logging
        (await app.Dispatcher.SendAsync(new EditPlanLogCommand(logged.PlanLogId, new LogEntry(10, true, 1, null))))
            .ShouldFailWith("planlog.edit_window");
        (await app.Dispatcher.QueryAsync(new GetLogResultQuery(logged.PlanLogId)))!.CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task SkippedSession_CountsAgainstConsistency_BR10()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var planId = await DailyPlanAsync(app, goal);
        var monday = await SessionOnAsync(app, planId, TestApp.Today);
        (await app.Dispatcher.SendAsync(new SkipOccurrenceCommand(monday, SkipReason.Sick))).ShouldSucceed();

        AdvanceDays(app, 1);
        var tuesday = await SessionOnAsync(app, planId, TestApp.Today.AddDays(1));
        var result = (await app.Dispatcher.SendAsync(new LogPlanResultCommand(tuesday, new LogEntry(1, true, 5, null)))).ShouldSucceed();

        result.Consistency.Should().Be(50); // 1 done of 2 scheduled in the last 7 days
        result.Score.Should().Be(88);       // 45 + 30 + 12.5 = 87.5 → 88

        var today = await app.Dispatcher.QueryAsync(new GetTodayQuery());
        today.Week.Single(d => d.Date == TestApp.Today).Planned.Should().Be(0); // skipped is not part of the day's goal
        (await app.Dispatcher.SendAsync(new SkipOccurrenceCommand(tuesday, SkipReason.Busy))).ShouldFailWith("occurrence.already_done");
    }

    [Fact]
    public async Task Binary_NotDone_ScoresZeroCompletion_AndBreaksStreak()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var planId = await DailyPlanAsync(app, goal);

        var result = (await app.Dispatcher.SendAsync(new LogPlanResultCommand(await SessionOnAsync(app, planId, TestApp.Today),
            new LogEntry(0, false, 3, null)))).ShouldSucceed();

        result.Completion.Should().Be(0);
        result.Consistency.Should().Be(0);
        result.Score.Should().Be(18); // 0 + 18 + 0
        result.CurrentStreak.Should().Be(0);
    }

    [Fact]
    public async Task Streak_CountsConsecutiveDoneDays()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var planId = await DailyPlanAsync(app, goal);
        for (int day = 0; day < 3; day++)
        {
            await app.Dispatcher.SendAsync(new LogPlanResultCommand(await SessionOnAsync(app, planId, TestApp.Today.AddDays(day)),
                new LogEntry(1, true, 4, null)));
            if (day < 2)
                AdvanceDays(app, 1);
        }

        (await app.Dispatcher.QueryAsync(new GetTodayQuery())).Streak.Should().Be(new Domain.Gamification.Streak(3, 3));
    }

    [Fact]
    public async Task Logging_UpdatesPlanMilestoneAndGoalProgress()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(start: TestApp.Today, target: TestApp.Today.AddDays(30));
        var milestone = (await app.Dispatcher.SendAsync(new AddMilestoneCommand(goal, "M1"))).ShouldSucceed();
        var plan = (await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "Daily", PlanType.Binary, 1, null,
            TestApp.Today, TestApp.Today.AddDays(9), Difficulty.Normal, MilestoneId: milestone))).ShouldSucceed();

        await app.Dispatcher.SendAsync(new LogPlanResultCommand(await SessionOnAsync(app, plan.PlanId, TestApp.Today),
            new LogEntry(1, true, 4, null)));

        var detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!;
        detail.Milestones.Single().Plans.Single().Progress.Should().Be(10); // 1 of 10 sessions at 100% (BR-08)
        detail.Milestones.Single().Progress.Should().Be(10);
        detail.Progress.Should().Be(10);
        detail.Score.Should().Be(detail.Milestones.Single().Plans.Single().AverageScore);
        detail.Status.Should().Be(GoalStatus.Active); // fewer than 7 days of data: not judged yet (BR-13)
    }

    [Fact]
    public async Task Goal_BehindExpected_AfterAWeek_BecomesAtRisk_FR50()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(start: TestApp.Today.AddDays(-30), target: TestApp.Today.AddDays(70));
        var planId = await DailyPlanAsync(app, goal, days: 60);

        AdvanceDays(app, 7);
        await app.Dispatcher.SendAsync(new LogPlanResultCommand(await SessionOnAsync(app, planId, TestApp.Today.AddDays(7)),
            new LogEntry(1, true, 4, null)));

        var page = await app.Dispatcher.QueryAsync(new GetGoalsQuery());
        var card = page.Goals.Single();
        card.Status.Should().Be(GoalStatus.AtRisk); // ~2% done vs 37% expected
        page.Summary.AtRisk.Should().Be(1);
    }

    [Fact]
    public async Task Goal_KeepingPace_IsOnTrack()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(start: TestApp.Today, target: TestApp.Today.AddDays(10));
        var planId = await DailyPlanAsync(app, goal, days: 10);
        for (int day = 0; day <= 7; day++)
        {
            await app.Dispatcher.SendAsync(new LogPlanResultCommand(await SessionOnAsync(app, planId, TestApp.Today.AddDays(day)),
                new LogEntry(1, true, 4, null)));
            if (day < 7)
                AdvanceDays(app, 1);
        }

        var detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!;
        detail.Progress.Should().Be(80);
        detail.ExpectedProgress.Should().Be(70);
        detail.Status.Should().Be(GoalStatus.OnTrack);
    }

    [Fact]
    public async Task PlanForTwoGoals_SplitsXpAcrossTheirAreas()
    {
        await using var app = await TestApp.CreateAsync();
        var english = await app.CreateGoalAsync();
        var career = await app.CreateGoalAsync("Senior", "Career");
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(english) with { AdditionalGoals = [(career, 1m)] })).ShouldSucceed();

        var result = (await app.Dispatcher.SendAsync(new LogPlanResultCommand(await SessionOnAsync(app, plan.PlanId, TestApp.Today),
            new LogEntry(20, true, 4, null)))).ShouldSucceed();

        var today = await app.Dispatcher.QueryAsync(new GetTodayQuery());
        today.Areas.Single(a => a.Name == "English").Level.TotalXp.Should().Be(result.Xp / 2);
        today.Areas.Single(a => a.Name == "Career").Level.TotalXp.Should().Be(result.Xp / 2);
        today.OverallLevel.TotalXp.Should().Be(result.Xp);
    }

    [Fact]
    public async Task Achievement_TicksCriteria_AndProgressIsCriteriaShare()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "IELTS", PlanType.Achievement, 0, null,
            TestApp.Today, TestApp.Today.AddDays(30), Difficulty.Hard, WeekDays.Monday,
            Criteria: ["Đăng ký", "Mock test", "Thi"]))).ShouldSucceed();
        var session = await SessionOnAsync(app, plan.PlanId, TestApp.Today);
        var form = (await app.Dispatcher.QueryAsync(new GetPlanResultFormQuery(session)))!;
        form.Criteria.Should().HaveCount(3);
        form.CanLog.Should().BeTrue();

        var result = (await app.Dispatcher.SendAsync(new LogPlanResultCommand(session,
            new LogEntry(0, true, 4, null, [form.Criteria[0].Id])))).ShouldSucceed();

        result.Completion.Should().BeApproximately(33.33m, 0.01m);
        var detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!;
        detail.UnassignedPlans.Single().CriteriaDone.Should().Be(1);
        detail.Progress.Should().BeApproximately(33.33m, 0.01m);
        (await app.Dispatcher.QueryAsync(new GetPlanResultFormQuery(session)))!.Log!.Score.Should().Be(result.Score);
    }

    [Fact]
    public async Task SkipFutureSession_Fails()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var planId = await DailyPlanAsync(app, goal);

        (await app.Dispatcher.SendAsync(new SkipOccurrenceCommand(await SessionOnAsync(app, planId, TestApp.Today.AddDays(1)), SkipReason.Busy)))
            .ShouldFailWith("occurrence.future_date");
        (await app.Dispatcher.SendAsync(new SkipOccurrenceCommand(Guid.NewGuid(), SkipReason.Busy))).ShouldFailWith("occurrence.not_found");
        (await app.Dispatcher.SendAsync(new EditPlanLogCommand(Guid.NewGuid(), new LogEntry(1, true, 3, null)))).ShouldFailWith("planlog.not_found");
        (await app.Dispatcher.QueryAsync(new GetPlanResultFormQuery(Guid.NewGuid()))).Should().BeNull();
    }
}
