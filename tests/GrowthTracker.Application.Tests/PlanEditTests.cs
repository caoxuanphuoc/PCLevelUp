using GrowthTracker.Application.Calendar;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Application.Reviews;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Tests;

/// <summary>"Đã kết thúc" plans, overdue milestones and "Sửa kế hoạch".</summary>
public class PlanEditTests
{
    private static readonly DateOnly Monday = TestApp.Today;

    private static async Task<Guid> CreatePlanAsync(TestApp app, CreatePlanCommand command) =>
        (await app.Dispatcher.SendAsync(command)).ShouldSucceed().PlanId;

    private static async Task LogAsync(TestApp app, Guid planId, DateOnly date, LogEntry entry)
    {
        app.Clock.UtcNow = date.ToDateTime(new TimeOnly(19, 0), DateTimeKind.Utc);
        var id = await app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == planId && o.ScheduledDate == date).Select(o => o.Id).SingleAsync());
        (await app.Dispatcher.SendAsync(new LogPlanResultCommand(id, entry))).ShouldSucceed();
    }

    private static Task<List<PlanOccurrence>> UpcomingAsync(TestApp app, Guid planId, DateOnly after) =>
        app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == planId && o.ScheduledDate > after)
                                                .OrderBy(o => o.ScheduledDate).ToListAsync());

    private static UpdatePlanCommand Edit(Guid planId, string name, DateOnly end, WeekDays days, TimeOnly? time = null,
                                          Guid? milestoneId = null, decimal target = 1) =>
        new(planId, name, target, null, end, Difficulty.Easy, days, PreferredTime: time, MilestoneId: milestoneId);

    [Fact]
    public async Task EndedPlan_IsShownAsEnded_AndExtendingItBringsItBack()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(target: Monday.AddDays(60));
        var milestone = (await app.Dispatcher.SendAsync(new AddMilestoneCommand(goal, "Week one", Monday.AddDays(3)))).ShouldSucceed();
        var read = await CreatePlanAsync(app, new(goal, "Read", PlanType.Binary, 1, null, Monday, Monday.AddDays(6), Difficulty.Easy,
                                                  WeekDays.All, MilestoneId: milestone));
        await LogAsync(app, read, Monday, new LogEntry(1, true, 4, null));
        await LogAsync(app, read, Monday.AddDays(1), new LogEntry(1, true, 4, null));
        app.Clock.UtcNow = Monday.AddDays(8).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc); // next Tuesday

        var detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!;
        var plan = detail.Milestones.Single().Plans.Single();
        plan.HasEnded.Should().BeTrue();
        plan.MayBeTooHard.Should().BeFalse(); // 2/7 done would be "too hard" (FR-51), but the plan is over
        plan.Progress.Should().BeApproximately(200m / 7, 0.01m);
        detail.Milestones.Single().IsOverdue.Should().BeTrue(); // due last Thursday, not completed
        (await app.Dispatcher.QueryAsync(new GetGoalsQuery())).Summary.ActivePlans.Should().Be(0);
        (await app.Dispatcher.QueryAsync(new GetSchedulablePlansQuery())).Should().BeEmpty();
        (await app.Dispatcher.SendAsync(new AddSessionCommand(read, Monday.AddDays(9)))).ShouldFailWith("plan.session_after_end");

        var result = (await app.Dispatcher.SendAsync(Edit(read, "Read", Monday.AddDays(13), WeekDays.All, milestoneId: milestone))).ShouldSucceed();

        result.SessionsAdded.Should().Be(6); // today (Tuesday) to Sunday; never the past (FR-24)
        result.SessionsRemoved.Should().Be(0);
        (await UpcomingAsync(app, read, Monday.AddDays(7))).Select(o => o.ScheduledDate).Should()
            .Equal(Enumerable.Range(8, 6).Select(d => Monday.AddDays(d)));
        detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!;
        plan = detail.Milestones.Single().Plans.Single();
        plan.HasEnded.Should().BeFalse();
        plan.Progress.Should().BeApproximately(200m / 14, 0.01m); // the Duration now has 14 sessions
        detail.Milestones.Single().Progress.Should().BeApproximately(200m / 14, 0.01m);
        (await app.Dispatcher.QueryAsync(new GetGoalsQuery())).Summary.ActivePlans.Should().Be(1);
    }

    [Fact]
    public async Task NewDays_RescheduleFromTomorrow_TodayAndLogsStay_BR14()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var listening = (await app.Dispatcher.SendAsync(Plans.Listening(goal) with { DurationEnd = Monday.AddDays(27) })).ShouldSucceed().PlanId;
        await LogAsync(app, listening, Monday, new LogEntry(20, true, 4, null));

        var result = (await app.Dispatcher.SendAsync(new UpdatePlanCommand(
            listening, "Listening", 25, "phút", Monday.AddDays(13), Difficulty.Hard,
            WeekDays.Tuesday | WeekDays.Thursday, PreferredTime: new TimeOnly(6, 0)))).ShouldSucceed();

        result.SessionsRemoved.Should().Be(11); // W/F of this week and the next three weeks
        result.SessionsAdded.Should().Be(4);    // Tue/Thu up to the new end date
        var upcoming = await UpcomingAsync(app, listening, Monday);
        upcoming.Select(o => o.ScheduledDate).Should().Equal(Monday.AddDays(1), Monday.AddDays(3), Monday.AddDays(8), Monday.AddDays(10));
        upcoming.Should().OnlyContain(o => o.ScheduledTime == new TimeOnly(6, 0));
        var today = await app.WithDbAsync(db => db.PlanOccurrences.SingleAsync(o => o.PlanId == listening && o.ScheduledDate == Monday));
        today.State.Should().Be(OccurrenceState.Done);
        var plan = await app.WithDbAsync(db => db.Plans.SingleAsync(p => p.Id == listening));
        (plan.TargetValue, plan.Difficulty, plan.PlanType).Should().Be((25m, Difficulty.Hard, PlanType.Time));
    }

    [Fact]
    public async Task EarlierEndDate_RemovesLaterSessions_NewTimeFollowsUnlessSetByHand()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var read = await CreatePlanAsync(app, new(goal, "Read", PlanType.Binary, 1, null, Monday, Monday.AddDays(27), Difficulty.Easy,
                                                  WeekDays.All, PreferredTime: new TimeOnly(21, 0)));
        var wednesday = await app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == read && o.ScheduledDate == Monday.AddDays(2)).Select(o => o.Id).SingleAsync());
        (await app.Dispatcher.SendAsync(new RescheduleOccurrenceCommand(wednesday, Monday.AddDays(2), NewTime: new TimeOnly(18, 0)))).ShouldSucceed();

        var result = (await app.Dispatcher.SendAsync(Edit(read, "Read", Monday.AddDays(6), WeekDays.All, time: new TimeOnly(22, 0)))).ShouldSucceed();

        (result.SessionsAdded, result.SessionsRemoved).Should().Be((0, 21));
        var sessions = await UpcomingAsync(app, read, Monday.AddDays(-1));
        sessions.Should().HaveCount(7);
        sessions.Single(o => o.Id == wednesday).ScheduledTime.Should().Be(new TimeOnly(18, 0)); // moved by hand: kept
        sessions.Where(o => o.Id != wednesday).Should().OnlyContain(o => o.ScheduledTime == new TimeOnly(22, 0));
    }

    [Fact]
    public async Task MovingToAnotherMilestone_RecomputesBoth()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var first = (await app.Dispatcher.SendAsync(new AddMilestoneCommand(goal, "First"))).ShouldSucceed();
        var second = (await app.Dispatcher.SendAsync(new AddMilestoneCommand(goal, "Second"))).ShouldSucceed();
        var read = await CreatePlanAsync(app, new(goal, "Read", PlanType.Binary, 1, null, Monday, Monday.AddDays(9), Difficulty.Easy,
                                                  WeekDays.All, MilestoneId: first));
        await LogAsync(app, read, Monday, new LogEntry(1, true, 4, null));

        (await app.Dispatcher.SendAsync(Edit(read, "Read", Monday.AddDays(9), WeekDays.All, milestoneId: second))).ShouldSucceed();

        var milestones = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!.Milestones;
        milestones.Single(m => m.Id == first).Should().Match<MilestoneDto>(m => m.Progress == 0 && m.Plans.Count == 0);
        milestones.Single(m => m.Id == second).Progress.Should().Be(10m); // 1 of 10 sessions

        var otherGoal = await app.CreateGoalAsync("Other", "Career");
        var foreign = (await app.Dispatcher.SendAsync(new AddMilestoneCommand(otherGoal, "Elsewhere"))).ShouldSucceed();
        (await app.Dispatcher.SendAsync(Edit(read, "Read", Monday.AddDays(9), WeekDays.All, milestoneId: foreign))).ShouldFailWith("milestone.not_found");
    }

    [Fact]
    public async Task EndedPlan_CanBeRenamed_ButNotGivenAPastEndDate()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var read = await CreatePlanAsync(app, new(goal, "Read", PlanType.Binary, 1, null, Monday, Monday.AddDays(6), Difficulty.Easy, WeekDays.All));
        app.Clock.UtcNow = Monday.AddDays(10).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);

        (await app.Dispatcher.SendAsync(Edit(read, "Read books", Monday.AddDays(6), WeekDays.All))).ShouldSucceed();
        (await app.Dispatcher.SendAsync(Edit(read, "Read", Monday.AddDays(8), WeekDays.All))).ShouldFailWith("plan.end_in_past");
        (await app.Dispatcher.SendAsync(Edit(Guid.NewGuid(), "Read", Monday.AddDays(20), WeekDays.All))).ShouldFailWith("plan.not_found");
    }

    [Fact]
    public async Task EndedPlan_CannotTakeASuggestion()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var listening = (await app.Dispatcher.SendAsync(Plans.Listening(goal) with { DurationEnd = Monday.AddDays(6) })).ShouldSucceed().PlanId;
        var suggestion = await app.WithDbAsync(async db =>
        {
            var r = new Recommendation(listening, RecommendationKind.IncreaseTarget, 20, 30, "test");
            db.Recommendations.Add(r);
            await db.SaveChangesAsync();
            return r.Id;
        });
        app.Clock.UtcNow = Monday.AddDays(7).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);

        (await app.Dispatcher.SendAsync(new ApplyRecommendationCommand(suggestion))).ShouldFailWith("plan.ended");
    }
}
