using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Plans;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Tests;

public class AreaUseCaseTests
{
    [Fact]
    public async Task Areas_CreateRenameArchiveRestore()
    {
        await using var app = await TestApp.CreateAsync();

        var id = (await app.Dispatcher.SendAsync(new CreateAreaCommand("Side Projects", "#334155"))).ShouldSucceed();
        (await app.Dispatcher.SendAsync(new UpdateAreaCommand(id, "Side Hustle", "#0f766e"))).ShouldSucceed();
        (await app.Dispatcher.SendAsync(new SetAreaArchivedCommand(id, true))).ShouldSucceed();

        var visible = await app.Dispatcher.QueryAsync(new GetAreasQuery());
        visible.Should().HaveCount(6).And.NotContain(a => a.Id == id);

        var all = await app.Dispatcher.QueryAsync(new GetAreasQuery(IncludeArchived: true));
        all.Single(a => a.Id == id).Should().Match<AreaDto>(a => a.Name == "Side Hustle" && a.ColorHex == "#0F766E" && a.SortOrder == 6);

        (await app.Dispatcher.SendAsync(new SetAreaArchivedCommand(id, false))).ShouldSucceed();
        (await app.Dispatcher.QueryAsync(new GetAreasQuery())).Should().HaveCount(7);
    }

    [Fact]
    public async Task Areas_InvalidInput_FailsWithDomainCode()
    {
        await using var app = await TestApp.CreateAsync();

        (await app.Dispatcher.SendAsync(new CreateAreaCommand("", "#334155"))).ShouldFailWith("area.name_required");
        (await app.Dispatcher.SendAsync(new CreateAreaCommand("X", "blue"))).ShouldFailWith("area.color");
        (await app.Dispatcher.SendAsync(new UpdateAreaCommand(Guid.NewGuid(), "X", "#000000"))).ShouldFailWith("area.not_found");
    }

    [Fact]
    public async Task Areas_LastActiveAreaCannotBeArchived()
    {
        await using var app = await TestApp.CreateAsync();
        var areas = await app.Dispatcher.QueryAsync(new GetAreasQuery());
        foreach (var area in areas.Skip(1))
            (await app.Dispatcher.SendAsync(new SetAreaArchivedCommand(area.Id, true))).ShouldSucceed();

        (await app.Dispatcher.SendAsync(new SetAreaArchivedCommand(areas[0].Id, true))).ShouldFailWith("area.last_active");
    }

    [Fact]
    public async Task Areas_CountActiveGoals()
    {
        await using var app = await TestApp.CreateAsync();
        await app.CreateGoalAsync("A");
        var cancelled = await app.CreateGoalAsync("B");
        await app.Dispatcher.SendAsync(new CancelGoalCommand(cancelled));

        var english = (await app.Dispatcher.QueryAsync(new GetAreasQuery())).Single(a => a.Name == "English");
        english.ActiveGoalCount.Should().Be(1);
    }
}

public class GoalUseCaseTests
{
    [Fact]
    public async Task CreateGoal_StartsAsDraft_WithExpectedProgress()
    {
        await using var app = await TestApp.CreateAsync();
        var id = await app.CreateGoalAsync(start: TestApp.Today.AddDays(-50), target: TestApp.Today.AddDays(50));

        var page = await app.Dispatcher.QueryAsync(new GetGoalsQuery());

        var card = page.Goals.Should().ContainSingle().Which;
        card.Id.Should().Be(id);
        card.Status.Should().Be(GoalStatus.Draft);
        card.ExpectedProgress.Should().Be(50m);
        card.DaysRemaining.Should().Be(50);
        card.AreaName.Should().Be("English");
    }

    [Fact]
    public async Task CreateGoal_InvalidFields_Fail()
    {
        await using var app = await TestApp.CreateAsync();
        var area = await app.AreaIdAsync("Career");

        (await app.Dispatcher.SendAsync(new CreateGoalCommand(new GoalFields(area, "G", TestApp.Today, TestApp.Today, 1))))
            .ShouldFailWith("goal.dates");
        (await app.Dispatcher.SendAsync(new CreateGoalCommand(new GoalFields(Guid.NewGuid(), "G", TestApp.Today, TestApp.Today.AddDays(1), 1))))
            .ShouldFailWith("area.not_found");
    }

    [Fact]
    public async Task UpdateGoal_ChangesFields()
    {
        await using var app = await TestApp.CreateAsync();
        var id = await app.CreateGoalAsync();
        var career = await app.AreaIdAsync("Career");

        (await app.Dispatcher.SendAsync(new UpdateGoalCommand(id,
            new GoalFields(career, "Senior .NET", TestApp.Today, TestApp.Today.AddYears(2), 1, "desc", "Offer letter"))))
            .ShouldSucceed();

        var detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(id)))!;
        detail.Name.Should().Be("Senior .NET");
        detail.AreaName.Should().Be("Career");
        detail.TargetDescription.Should().Be("Offer letter");
        (await app.Dispatcher.SendAsync(new UpdateGoalCommand(Guid.NewGuid(),
            new GoalFields(career, "x", TestApp.Today, TestApp.Today.AddDays(1), 1)))).ShouldFailWith("goal.not_found");
    }

    [Fact]
    public async Task GetGoals_FiltersByArea_HidesCancelledByDefault_AndSortsAtRiskFirst()
    {
        await using var app = await TestApp.CreateAsync();
        var low = await app.CreateGoalAsync("Low priority", "Career", priority: 4);
        var high = await app.CreateGoalAsync("High priority", "Career", priority: 1);
        var risky = await app.CreateGoalAsync("Risky", "Career", priority: 3);
        var cancelled = await app.CreateGoalAsync("Dropped", "Health");
        await app.Dispatcher.SendAsync(new CancelGoalCommand(cancelled));
        await app.WithDbAsync(async db =>
        {
            var goal = await db.Goals.SingleAsync(g => g.Id == risky);
            goal.Activate();
            goal.ApplyProgress(10, 40, null, isAtRisk: true);
            return await db.SaveChangesAsync();
        });

        var page = await app.Dispatcher.QueryAsync(new GetGoalsQuery());
        page.Goals.Select(g => g.Id).Should().Equal(risky, high, low);
        page.Summary.Should().Be(new GoalsSummaryDto(OpenGoals: 3, AreasWithGoals: 1, AtRisk: 1, OnTrack: 0, ActivePlans: 0));
        page.AreaFilters.Single(a => a.Name == "Career").GoalCount.Should().Be(3);
        page.AreaFilters.Single(a => a.Name == "Health").GoalCount.Should().Be(0);

        var health = await app.AreaIdAsync("Health");
        (await app.Dispatcher.QueryAsync(new GetGoalsQuery(AreaId: health))).Goals.Should().BeEmpty();
        (await app.Dispatcher.QueryAsync(new GetGoalsQuery(health, [GoalStatus.Cancelled]))).Goals
            .Should().ContainSingle(g => g.Id == cancelled);
    }

    [Fact]
    public async Task Milestones_AddInOrder_AndRemoveDetachesPlans()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var m1 = (await app.Dispatcher.SendAsync(new AddMilestoneCommand(goal, "Vocabulary"))).ShouldSucceed();
        var m2 = (await app.Dispatcher.SendAsync(new AddMilestoneCommand(goal, "Listening", TestApp.Today.AddMonths(2), 2m))).ShouldSucceed();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal, m2))).ShouldSucceed();

        var detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!;
        detail.Milestones.Select(m => m.Id).Should().Equal(m1, m2);
        detail.Milestones[1].Plans.Should().ContainSingle(p => p.Id == plan.PlanId);
        detail.UnassignedPlans.Should().BeEmpty();

        (await app.Dispatcher.SendAsync(new RemoveMilestoneCommand(m2))).ShouldSucceed();

        detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!;
        detail.Milestones.Should().ContainSingle(m => m.Id == m1);
        detail.UnassignedPlans.Should().ContainSingle(p => p.Id == plan.PlanId);
        (await app.Dispatcher.SendAsync(new RemoveMilestoneCommand(m2))).ShouldFailWith("milestone.not_found");
    }

    [Fact]
    public async Task ClosedGoal_RejectsNewMilestonesAndPlans()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        await app.Dispatcher.SendAsync(new CancelGoalCommand(goal));

        (await app.Dispatcher.SendAsync(new AddMilestoneCommand(goal, "M"))).ShouldFailWith("goal.closed");
        (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldFailWith("goal.closed");
    }

    [Fact]
    public async Task GetGoalDetail_UnknownGoal_ReturnsNull()
    {
        await using var app = await TestApp.CreateAsync();

        (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(Guid.NewGuid()))).Should().BeNull();
    }
}

public class PlanUseCaseTests
{
    [Fact]
    public async Task CreatePlan_SchedulesEightWeeks_AndActivatesGoal()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();

        var result = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();

        result.SessionsScheduled.Should().Be(24); // Mon/Wed/Fri × 8 weeks
        result.FirstSession.Should().Be(TestApp.Today);
        var detail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!;
        detail.Status.Should().Be(GoalStatus.Active);
        var plan = detail.UnassignedPlans.Should().ContainSingle().Which;
        plan.UpcomingSessions.Should().Be(24);
        plan.NextSession.Should().Be(TestApp.Today);
        plan.ScheduledDays.Should().Be(WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday);
        plan.Progress.Should().Be(0);
        plan.AverageScore.Should().BeNull();

        var page = await app.Dispatcher.QueryAsync(new GetGoalsQuery());
        page.Goals.Single().ActivePlanCount.Should().Be(1);
        page.Summary.ActivePlans.Should().Be(1);
    }

    [Fact]
    public async Task CreatePlan_FrequencyWithoutDays_SpreadsAcrossWeek()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync("Fit", "Health");

        var result = (await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "Gym", PlanType.Frequency, 3, "buổi",
            TestApp.Today, TestApp.Today.AddDays(13), Difficulty.Hard, FrequencyPerWeek: 3))).ShouldSucceed();

        result.SessionsScheduled.Should().Be(6);
    }

    [Fact]
    public async Task CreatePlan_Achievement_StoresCriteria()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();

        var result = (await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "IELTS", PlanType.Achievement, 0, null,
            TestApp.Today, TestApp.Today.AddDays(30), Difficulty.Extreme, WeekDays.Saturday,
            Criteria: ["Register", "Take the exam"]))).ShouldSucceed();

        var plan = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!.UnassignedPlans.Single();
        plan.Id.Should().Be(result.PlanId);
        plan.CriteriaTotal.Should().Be(2);
    }

    [Fact]
    public async Task CreatePlan_ForSeveralGoals_ShowsOnEachGoal()
    {
        await using var app = await TestApp.CreateAsync();
        var english = await app.CreateGoalAsync();
        var career = await app.CreateGoalAsync("Senior", "Career");

        var result = (await app.Dispatcher.SendAsync(Plans.Listening(english) with { AdditionalGoals = [(career, 1m)] })).ShouldSucceed();

        var careerDetail = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(career)))!;
        careerDetail.Status.Should().Be(GoalStatus.Active);
        careerDetail.UnassignedPlans.Single().Should().Match<PlanSummaryDto>(p => p.Id == result.PlanId && p.LinkedGoalCount == 2);
    }

    [Fact]
    public async Task CreatePlan_Validation()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var other = await app.CreateGoalAsync("Other");
        var foreignMilestone = (await app.Dispatcher.SendAsync(new AddMilestoneCommand(other, "M"))).ShouldSucceed();

        (await app.Dispatcher.SendAsync(Plans.Listening(goal) with { TargetValue = 0 })).ShouldFailWith("plan.target_required");
        (await app.Dispatcher.SendAsync(Plans.Listening(goal, foreignMilestone))).ShouldFailWith("milestone.not_found");
        (await app.Dispatcher.SendAsync(Plans.Listening(Guid.NewGuid()))).ShouldFailWith("goal.not_found");
    }

    [Fact]
    public async Task CreatePlan_StartingInThePast_SchedulesFromToday()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();

        var result = (await app.Dispatcher.SendAsync(Plans.Listening(goal) with { DurationStart = TestApp.Today.AddDays(-30) })).ShouldSucceed();

        result.FirstSession.Should().Be(TestApp.Today); // FR-24
    }

    [Fact]
    public async Task ArchivePlan_RemovesUpcomingSessions_KeepsPlanVisibleAsArchived()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();

        (await app.Dispatcher.SendAsync(new ArchivePlanCommand(plan.PlanId))).ShouldSucceed();

        var summary = (await app.Dispatcher.QueryAsync(new GetGoalDetailQuery(goal)))!.UnassignedPlans.Single();
        summary.Status.Should().Be(PlanStatus.Archived);
        summary.UpcomingSessions.Should().Be(0);
        (await app.Dispatcher.QueryAsync(new GetGoalsQuery())).Summary.ActivePlans.Should().Be(0);
        (await app.Dispatcher.SendAsync(new ArchivePlanCommand(Guid.NewGuid()))).ShouldFailWith("plan.not_found");
    }

    [Fact]
    public async Task CancelGoal_ArchivesExclusivePlans_ButKeepsSharedOnes()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var other = await app.CreateGoalAsync("Other", "Career");
        var exclusive = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        var shared = (await app.Dispatcher.SendAsync(Plans.Listening(goal) with { Name = "Shared", AdditionalGoals = [(other, 1m)] })).ShouldSucceed();

        (await app.Dispatcher.SendAsync(new CancelGoalCommand(goal))).ShouldSucceed();

        var statuses = await app.WithDbAsync(db => db.Plans.ToDictionaryAsync(p => p.Id, p => p.Status));
        statuses[exclusive.PlanId].Should().Be(PlanStatus.Archived);
        statuses[shared.PlanId].Should().Be(PlanStatus.Active);
        (await app.Dispatcher.SendAsync(new CancelGoalCommand(Guid.NewGuid()))).ShouldFailWith("goal.not_found");
    }
}

internal static class Plans
{
    /// <summary>Listening 20 min, Mon/Wed/Fri, for 3 months.</summary>
    public static CreatePlanCommand Listening(Guid goalId, Guid? milestoneId = null) =>
        new(goalId, "Listening (Tech Pods)", PlanType.Time, 20, "phút", TestApp.Today, TestApp.Today.AddMonths(3),
            Difficulty.Normal, WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday,
            PreferredTime: new TimeOnly(7, 30), MilestoneId: milestoneId);
}
