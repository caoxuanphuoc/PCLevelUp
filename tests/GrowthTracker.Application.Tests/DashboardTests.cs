using GrowthTracker.Application.Dashboard;
using GrowthTracker.Application.Jobs;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Progress;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Tests;

public class DashboardTests
{
    /// <summary>
    /// An English goal with a daily Easy Binary plan. Week 1 (Mon–Sun) is logged with 3 stars (Score 88),
    /// then Mon–Wed of week 2 with 5 stars (Score 100). The clock ends on Wednesday of week 2.
    /// </summary>
    private static async Task<(TestApp App, Guid GoalId)> TwoWeeksAsync()
    {
        var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(start: TestApp.Today, target: TestApp.Today.AddDays(60));
        var plan = (await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "Daily", PlanType.Binary, 1, null,
            TestApp.Today, TestApp.Today.AddDays(59), Difficulty.Easy))).ShouldSucceed().PlanId;
        for (int day = 0; day <= 9; day++)
        {
            var date = TestApp.Today.AddDays(day);
            app.Clock.UtcNow = date.ToDateTime(new TimeOnly(20, 0), DateTimeKind.Utc);
            var session = await app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == plan && o.ScheduledDate == date)
                                                                         .Select(o => o.Id).SingleAsync());
            await app.Dispatcher.SendAsync(new LogPlanResultCommand(session, new LogEntry(1, true, day < 7 ? 3 : 5, null)));
        }
        return (app, goal);
    }

    [Fact]
    public async Task NewUser_HasNoNumbers()
    {
        await using var app = await TestApp.CreateAsync();

        var dashboard = await app.Dispatcher.QueryAsync(new GetDashboardSummaryQuery());

        dashboard.IsNewUser.Should().BeTrue();
        dashboard.OverallScore.Should().BeNull();
        dashboard.HasEnoughData.Should().BeFalse();
        dashboard.Areas.Should().BeEmpty();
        dashboard.ShowRiskBanner.Should().BeFalse();
    }

    [Fact]
    public async Task Summary_ComparesThisWeekWithLastWeek()
    {
        var (app, _) = await TwoWeeksAsync();
        await using var _ = app;

        var dashboard = await app.Dispatcher.QueryAsync(new GetDashboardSummaryQuery(DashboardPeriod.Week));

        dashboard.PeriodStart.Should().Be(TestApp.Today.AddDays(7));
        dashboard.OverallScore.Should().Be(100);
        dashboard.PreviousScore.Should().Be(88);
        dashboard.ScoreTrend.Should().Be(Trend.Improving);
        dashboard.Streak.Current.Should().Be(10);
        dashboard.DaysOfData.Should().Be(10);
        dashboard.HasEnoughData.Should().BeTrue();
        dashboard.GoalCount.Should().Be(1);
        dashboard.PlanCount.Should().Be(1);
        dashboard.OverallLevel.TotalXp.Should().Be(7 * 88 + 3 * 100);

        var english = dashboard.Areas.Should().ContainSingle().Which;
        english.Name.Should().Be("English");
        english.Score.Should().Be(100);
        english.Trend.Should().Be(Trend.Improving);
        english.Progress.Should().BeGreaterThan(0);
        english.Level.TotalXp.Should().Be(dashboard.OverallLevel.TotalXp);
    }

    [Fact]
    public async Task Periods_CoverMonthAndQuarter()
    {
        await using var app = await TestApp.CreateAsync();

        var month = await app.Dispatcher.QueryAsync(new GetDashboardSummaryQuery(DashboardPeriod.Month));
        var quarter = await app.Dispatcher.QueryAsync(new GetDashboardSummaryQuery(DashboardPeriod.Quarter));

        (month.PeriodStart, month.PeriodEnd).Should().Be((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
        (quarter.PeriodStart, quarter.PeriodEnd).Should().Be((new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public async Task Trend_SixWeeks_EndingThisWeek()
    {
        var (app, _) = await TwoWeeksAsync();
        await using var _ = app;

        var trend = await app.Dispatcher.QueryAsync(new GetScoreTrendQuery());

        trend.Points.Should().HaveCount(6);
        trend.Points[^1].WeekStart.Should().Be(TestApp.Today.AddDays(7));
        trend.Points.Select(p => p.AverageScore).Should().Equal(null, null, null, null, 88m, 100m);
        trend.Points[^2].Done.Should().Be(7);
        trend.Points[^1].Done.Should().Be(3);
        trend.Points[^1].Planned.Should().Be(7);
        trend.Trend.Should().Be(Trend.Improving);
        trend.HasEnoughData.Should().BeTrue();
    }

    [Fact]
    public async Task Trend_IsHiddenWithFewerThanSevenDaysOfData_BR13()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        var session = await app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == plan.PlanId && o.ScheduledDate == TestApp.Today)
                                                                     .Select(o => o.Id).SingleAsync());
        await app.Dispatcher.SendAsync(new LogPlanResultCommand(session, new LogEntry(20, true, 4, null)));

        var trend = await app.Dispatcher.QueryAsync(new GetScoreTrendQuery());
        var dashboard = await app.Dispatcher.QueryAsync(new GetDashboardSummaryQuery());

        trend.HasEnoughData.Should().BeFalse();
        trend.Trend.Should().BeNull();
        dashboard.HasEnoughData.Should().BeFalse();
        dashboard.OverallScore.Should().NotBeNull();
    }

    [Fact]
    public async Task RiskBanner_ShowsAtRiskGoals_UntilSnoozedForSevenDays()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(start: TestApp.Today, target: TestApp.Today.AddDays(20));
        await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "Daily", PlanType.Binary, 1, null,
            TestApp.Today, TestApp.Today.AddDays(19), Difficulty.Easy));
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(8);
        await app.Dispatcher.SendAsync(new RunRiskScanCommand());

        var dashboard = await app.Dispatcher.QueryAsync(new GetDashboardSummaryQuery());
        dashboard.ShowRiskBanner.Should().BeTrue();
        dashboard.AtRiskGoals.Single().Should().Match<RiskGoalDto>(g => g.GoalId == goal && g.ExpectedProgress == 40);
        dashboard.Areas.Single().HasAtRisk.Should().BeTrue();

        (await app.Dispatcher.SendAsync(new SnoozeRiskBannerCommand())).ShouldSucceed().Should().Be(TestApp.Today.AddDays(14));
        (await app.Dispatcher.QueryAsync(new GetDashboardSummaryQuery())).ShowRiskBanner.Should().BeFalse();

        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(7);
        (await app.Dispatcher.QueryAsync(new GetDashboardSummaryQuery())).ShowRiskBanner.Should().BeTrue();
    }
}
