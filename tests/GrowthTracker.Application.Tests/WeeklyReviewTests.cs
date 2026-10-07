using GrowthTracker.Application.Dashboard;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Jobs;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Application.Reviews;
using GrowthTracker.Application.Settings;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Tests;

public class WeeklyReviewTests
{
    private static readonly DateOnly Week4 = TestApp.Today.AddDays(21);   // Monday of the 4th week
    private static readonly DateOnly Sunday4 = TestApp.Today.AddDays(27);

    private sealed record Scenario(TestApp App, Guid GoalId, Guid Daily, Guid Gym, Guid Speaking);

    /// <summary>
    /// Four weeks. Daily (Binary, every day) is always done; Gym (5/week, Mon–Fri) only on Mon and Tue;
    /// Speaking (20 min Mon/Wed/Fri) always done in full. The clock ends on Sunday of week 4 at <paramref name="endHour"/>.
    /// </summary>
    private static async Task<Scenario> FourWeeksAsync(int endHour = 21)
    {
        var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync(start: TestApp.Today, target: TestApp.Today.AddDays(120));
        async Task<Guid> Plan(CreatePlanCommand c) => (await app.Dispatcher.SendAsync(c)).ShouldSucceed().PlanId;
        var end = TestApp.Today.AddDays(90);
        var daily = await Plan(new(goal, "Daily", PlanType.Binary, 1, null, TestApp.Today, end, Difficulty.Easy));
        var gym = await Plan(new(goal, "Gym", PlanType.Frequency, 5, "buổi", TestApp.Today, end, Difficulty.Hard, WeekDays.Weekdays, FrequencyPerWeek: 5));
        var speaking = await Plan(new(goal, "Speaking", PlanType.Time, 20, "phút", TestApp.Today, end, Difficulty.Normal,
                                      WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday));

        for (int day = 0; day < 28; day++)
        {
            var date = TestApp.Today.AddDays(day);
            app.Clock.UtcNow = date.ToDateTime(new TimeOnly(19, 0), DateTimeKind.Utc);
            await LogAsync(app, daily, date, new LogEntry(1, true, 4, null));
            if (date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Tuesday)
                await LogAsync(app, gym, date, new LogEntry(1, true, 3, null));
            if (date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday)
                await LogAsync(app, speaking, date, new LogEntry(20, true, 5, null));
        }
        app.Clock.UtcNow = Sunday4.ToDateTime(new TimeOnly(endHour, 0), DateTimeKind.Utc);
        return new Scenario(app, goal, daily, gym, speaking);
    }

    private static async Task LogAsync(TestApp app, Guid planId, DateOnly date, LogEntry entry)
    {
        var id = await app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == planId && o.ScheduledDate == date).Select(o => o.Id).SingleAsync());
        (await app.Dispatcher.SendAsync(new LogPlanResultCommand(id, entry))).ShouldSucceed();
    }

    [Fact]
    public async Task Job_GeneratesTheWeekAtSundayEight_OnlyOnce()
    {
        var s = await FourWeeksAsync(endHour: 19);
        await using var _ = s.App;

        (await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand())).ShouldSucceed()!.WeekStart.Should().Be(Week4.AddDays(-7)); // catch-up: week 3
        (await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand())).ShouldSucceed().Should().BeNull();

        s.App.Clock.UtcNow = s.App.Clock.UtcNow.AddHours(1); // 20:00
        var result = (await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand())).ShouldSucceed();
        result!.WeekStart.Should().Be(Week4);
        result.IsoWeek.Should().Be(44);
        (await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand())).ShouldSucceed().Should().BeNull();
    }

    [Fact]
    public async Task Review_ShowsWeekFigures_Areas_HighlightsAttentionAndSuggestions()
    {
        var s = await FourWeeksAsync();
        await using var _ = s.App;
        await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand());

        var review = await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(Week4));

        review.IsGenerated.Should().BeTrue();
        review.IsProvisional.Should().BeTrue(); // Sunday evening: the week is not over yet
        review.PlansTotal.Should().Be(7 + 5 + 3);
        review.PlansDone.Should().Be(7 + 2 + 3);
        review.ConsistencyPct.Should().Be(80);
        review.Streak.Should().Be(28);
        review.ActiveDays.Should().Be(7);
        review.XpGained.Should().BeGreaterThan(0);
        review.Areas.Should().ContainSingle().Which.Name.Should().Be("English");
        review.Highlights.Select(h => h.Title).Should().Contain("Daily: 7/7 buổi").And.Contain("Speaking: 3/3 buổi");
        review.Attention.Should().Contain(a => a.Title == "Gym có thể quá khó" && a.Detail.StartsWith("Chỉ đạt 4/10 buổi"));
        review.CanSuggest.Should().BeTrue();

        review.Suggestions.Should().HaveCount(2);
        var gym = review.Suggestions.Single(x => x.PlanName == "Gym");
        (gym.Kind, gym.CurrentValue, gym.SuggestedValue).Should().Be((RecommendationKind.DecreaseTarget, 5m, 2m));
        gym.Reason.Should().Be("Hoàn thành 40% trong 4 tuần gần nhất");
        var speaking = review.Suggestions.Single(x => x.PlanName == "Speaking");
        (speaking.Kind, speaking.CurrentValue, speaking.SuggestedValue).Should().Be((RecommendationKind.IncreaseTarget, 20m, 30m));
    }

    [Fact]
    public async Task ApplyFrequencySuggestion_ReschedulesFutureSessionsOnly_BR14()
    {
        var s = await FourWeeksAsync();
        await using var _ = s.App;
        await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand());
        var gym = (await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(Week4))).Suggestions.Single(x => x.PlanName == "Gym");

        (await s.App.Dispatcher.SendAsync(new ApplyRecommendationCommand(gym.Id))).ShouldSucceed();

        var next = await s.App.WithDbAsync(db => db.PlanOccurrences
            .Where(o => o.PlanId == s.Gym && o.ScheduledDate > Sunday4 && o.ScheduledDate <= Sunday4.AddDays(7))
            .Select(o => o.ScheduledDate).ToListAsync());
        next.Select(d => d.DayOfWeek).Should().BeEquivalentTo([DayOfWeek.Monday, DayOfWeek.Thursday]);
        var past = await s.App.WithDbAsync(db => db.PlanOccurrences.CountAsync(o => o.PlanId == s.Gym && o.ScheduledDate <= Sunday4));
        past.Should().Be(20); // history untouched
        (await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(Week4))).Suggestions.Single(x => x.Id == gym.Id)
            .State.Should().Be(RecommendationState.Applied);
        (await s.App.Dispatcher.SendAsync(new ApplyRecommendationCommand(gym.Id))).ShouldFailWith("recommendation.not_pending");
    }

    [Fact]
    public async Task ApplyTimeSuggestion_ChangesTarget()
    {
        var s = await FourWeeksAsync();
        await using var _ = s.App;
        await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand());
        var speaking = (await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(Week4))).Suggestions.Single(x => x.PlanName == "Speaking");

        await s.App.Dispatcher.SendAsync(new ApplyRecommendationCommand(speaking.Id));

        (await s.App.WithDbAsync(db => db.Plans.SingleAsync(p => p.Id == s.Speaking))).TargetValue.Should().Be(30);
    }

    [Fact]
    public async Task Dismiss_HidesForFourWeeks_FR54()
    {
        var s = await FourWeeksAsync();
        await using var _ = s.App;
        await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand());
        var speaking = (await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(Week4))).Suggestions.Single(x => x.PlanName == "Speaking");

        (await s.App.Dispatcher.SendAsync(new DismissRecommendationCommand(speaking.Id))).ShouldSucceed();
        await s.App.Dispatcher.SendAsync(new GenerateWeeklyReviewCommand(Week4)); // regenerate: no duplicate, no new Speaking suggestion

        var review = await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(Week4));
        review.Suggestions.Should().HaveCount(2);
        review.Suggestions.Single(x => x.PlanName == "Speaking").State.Should().Be(RecommendationState.Dismissed);
    }

    [Fact]
    public async Task Complete_SavesNote_AndStopsTheDashboardReminder()
    {
        var s = await FourWeeksAsync();
        await using var _ = s.App;
        await s.App.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand());
        s.App.Clock.UtcNow = s.App.Clock.UtcNow.AddDays(1); // Monday

        (await s.App.Dispatcher.QueryAsync(new GetDashboardSummaryQuery())).ReviewReminderWeek.Should().Be(Week4);
        (await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery())).WeekStart.Should().Be(Week4); // default: last week

        (await s.App.Dispatcher.SendAsync(new SaveWeeklyReviewNoteCommand(Week4, "  Tuần ổn  "))).ShouldSucceed();
        (await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(Week4))).Note.Should().Be("Tuần ổn");
        (await s.App.Dispatcher.SendAsync(new CompleteWeeklyReviewCommand(Week4, "Giữ nhịp Daily"))).ShouldSucceed();

        var review = await s.App.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(Week4));
        review.IsCompleted.Should().BeTrue();
        review.Note.Should().Be("Giữ nhịp Daily");
        review.IsProvisional.Should().BeFalse();
        (await s.App.Dispatcher.QueryAsync(new GetDashboardSummaryQuery())).ReviewReminderWeek.Should().BeNull();
    }

    [Fact]
    public async Task FewLoggedDays_NoSuggestions()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        await app.Dispatcher.SendAsync(Plans.Listening(goal));
        app.Clock.UtcNow = TestApp.Today.AddDays(6).ToDateTime(new TimeOnly(21, 0), DateTimeKind.Utc);
        (await app.Dispatcher.SendAsync(new RunWeeklyReviewJobCommand())).ShouldSucceed()!.WeekStart.Should().Be(TestApp.Today);

        var review = await app.Dispatcher.QueryAsync(new GetWeeklyReviewQuery(TestApp.Today));

        review.CanSuggest.Should().BeFalse();
        review.Suggestions.Should().BeEmpty();
        review.Score.Should().BeNull();
        review.CanGoNext.Should().BeFalse();
    }

    [Fact]
    public async Task GoalDetail_FlagsPlansThatMayBeTooHard_FR51()
    {
        var s = await FourWeeksAsync();
        await using var _ = s.App;

        var plans = (await s.App.Dispatcher.QueryAsync(new GetGoalDetailQuery(s.GoalId)))!.UnassignedPlans;

        plans.Single(p => p.Id == s.Gym).Should().Match<PlanSummaryDto>(p => p.MayBeTooHard && p.Consistency14Days == 40);
        plans.Single(p => p.Id == s.Daily).MayBeTooHard.Should().BeFalse();
    }

    [Fact]
    public async Task DailyBackup_RunsOncePerDay()
    {
        await using var app = await TestApp.CreateAsync();

        await app.Dispatcher.SendAsync(new RunDailyBackupCommand());

        (await app.Dispatcher.QueryAsync(new GetAppSettingsQuery())).LastBackupDate.Should().Be(TestApp.Today);
        (await app.Dispatcher.SendAsync(new RunDailyBackupCommand())).ShouldSucceed().Should().BeNull();
    }
}
