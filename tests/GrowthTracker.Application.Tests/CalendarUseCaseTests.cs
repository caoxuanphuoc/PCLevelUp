using GrowthTracker.Application.Calendar;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Jobs;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Tests;

public class CalendarUseCaseTests
{
    private static readonly DateOnly Monday = TestApp.Today;
    private static readonly DateOnly Sunday = TestApp.Today.AddDays(6);

    private static Task<Guid> SessionOnAsync(TestApp app, Guid planId, DateOnly date) =>
        app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == planId && o.ScheduledDate == date).Select(o => o.Id).SingleAsync());

    [Fact]
    public async Task Calendar_ListsSessionsWithArea_DeadlinesAndAreaChips()
    {
        await using var app = await TestApp.CreateAsync();
        var english = await app.CreateGoalAsync(target: Monday.AddDays(5));
        await app.Dispatcher.SendAsync(new AddMilestoneCommand(english, "Vocabulary", Monday.AddDays(3)));
        await app.Dispatcher.SendAsync(Plans.Listening(english));
        var career = await app.CreateGoalAsync("Senior", "Career", target: Monday.AddYears(1));
        await app.Dispatcher.SendAsync(new CreatePlanCommand(career, "Read", PlanType.Binary, 1, null, Monday, Monday.AddDays(30), Difficulty.Easy));

        var week = await app.Dispatcher.QueryAsync(new GetCalendarQuery(Monday, Sunday));

        week.Sessions.Should().HaveCount(3 + 7);
        week.Sessions.First().Should().Match<CalendarSessionDto>(s =>
            s.Date == Monday && s.Time == new TimeOnly(7, 30) && s.AreaName == "English" && s.DurationMinutes == 20);
        week.Areas.Select(a => (a.Name, a.SessionCount)).Should().Equal(("Career", 7), ("English", 3));
        week.Deadlines.Select(d => (d.Kind, d.Name, d.Date)).Should().Equal(
            (DeadlineKind.Milestone, "Vocabulary", Monday.AddDays(3)),
            (DeadlineKind.Goal, "Improve English", Monday.AddDays(5)));

        var englishOnly = await app.Dispatcher.QueryAsync(new GetCalendarQuery(Monday, Sunday, week.Areas.Single(a => a.Name == "English").AreaId));
        englishOnly.Sessions.Should().HaveCount(3).And.OnlyContain(s => s.AreaName == "English");
        englishOnly.Deadlines.Should().HaveCount(2);
        englishOnly.Areas.Should().HaveCount(2); // chips always show every Area of the range
    }

    [Fact]
    public async Task PeakDays_ExceedOneAndAHalfTimesTheAverageDay_FR25()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "Daily", PlanType.Binary, 1, null, Monday, Monday.AddDays(30), Difficulty.Easy));
        await app.Dispatcher.SendAsync(new CreatePlanCommand(goal, "Deep work", PlanType.Time, 120, "phút", Monday, Monday.AddDays(30),
                                                             Difficulty.Hard, WeekDays.Monday));

        var week = await app.Dispatcher.QueryAsync(new GetCalendarQuery(Monday, Sunday));

        week.PeakDays.Should().Equal(Monday); // 150 min vs a 47-minute average day
    }

    [Fact]
    public async Task Reschedule_ThisOnly_MovesOneSession_CountsTowardRescheduleRate()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        var wednesday = await SessionOnAsync(app, plan.PlanId, Monday.AddDays(2));

        var moved = (await app.Dispatcher.SendAsync(new RescheduleOccurrenceCommand(wednesday, Monday.AddDays(3), Reason: "Bận họp"))).ShouldSucceed();

        moved.Should().Be(1);
        var week = await app.Dispatcher.QueryAsync(new GetCalendarQuery(Monday, Sunday));
        var thursday = week.Sessions.Single(s => s.OccurrenceId == wednesday);
        thursday.Date.Should().Be(Monday.AddDays(3));
        thursday.OriginalDate.Should().Be(Monday.AddDays(2));
        thursday.IsMoved.Should().BeTrue();
        week.Stats.RescheduleRatePct.Should().Be(33.3m); // 1 of 3
        week.Sessions.Select(s => s.Date.DayOfWeek).Should().Equal(DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Friday);
    }

    [Fact]
    public async Task Reschedule_ThisAndFollowing_ShiftsTheRestOfThePlan_AndRolloverDoesNotRefill()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        var wednesday = await SessionOnAsync(app, plan.PlanId, Monday.AddDays(2));

        var moved = (await app.Dispatcher.SendAsync(new RescheduleOccurrenceCommand(wednesday, Monday.AddDays(3), RescheduleScope.ThisAndFollowing)))
            .ShouldSucceed();

        moved.Should().Be(23); // every session from Wednesday on, Monday stays
        var dates = await app.WithDbAsync(db => db.PlanOccurrences.Where(o => o.PlanId == plan.PlanId)
                                                                  .OrderBy(o => o.ScheduledDate).Select(o => o.ScheduledDate).ToListAsync());
        dates.Take(4).Select(d => d.DayOfWeek).Should().Equal(DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Saturday, DayOfWeek.Tuesday);

        (await app.Dispatcher.SendAsync(new RunRolloverCommand())).ShouldSucceed().SessionsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Reschedule_IntoThePast_OrALoggedSession_IsRejected()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        var monday = await SessionOnAsync(app, plan.PlanId, Monday);

        (await app.Dispatcher.SendAsync(new RescheduleOccurrenceCommand(monday, Monday.AddDays(-1)))).ShouldFailWith("occurrence.past_date");

        await app.Dispatcher.SendAsync(new LogPlanResultCommand(monday, new LogEntry(20, true, 4, null)));
        (await app.Dispatcher.SendAsync(new RescheduleOccurrenceCommand(monday, Monday.AddDays(1)))).ShouldFailWith("occurrence.not_scheduled");
        (await app.Dispatcher.SendAsync(new RescheduleOccurrenceCommand(Guid.NewGuid(), Monday))).ShouldFailWith("occurrence.not_found");
    }

    [Fact]
    public async Task Adherence_CountsSessionsDoneOnTheirScheduledDay()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        await app.Dispatcher.SendAsync(new LogPlanResultCommand(await SessionOnAsync(app, plan.PlanId, Monday), new LogEntry(20, true, 4, null)));

        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(3); // Thursday; Wednesday was not logged
        var week = await app.Dispatcher.QueryAsync(new GetCalendarQuery(Monday, Sunday));

        week.Stats.DueCount.Should().Be(2);
        week.Stats.DoneCount.Should().Be(1);
        week.Stats.AdherencePct.Should().Be(50);
        week.Stats.BestWeekday.Should().BeNull(); // fewer than 4 weeks of logs
    }

    [Fact]
    public async Task BestWeekday_AppearsAfterFourWeeksOfLogs()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        for (int week = 0; week < 4; week++)
        {
            foreach (var (offset, quality) in new[] { (0, 5), (2, 2), (4, 3) })
            {
                var date = Monday.AddDays(week * 7 + offset);
                app.Clock.UtcNow = date.ToDateTime(new TimeOnly(20, 0), DateTimeKind.Utc);
                await app.Dispatcher.SendAsync(new LogPlanResultCommand(await SessionOnAsync(app, plan.PlanId, date), new LogEntry(20, true, quality, null)));
            }
        }

        var calendar = await app.Dispatcher.QueryAsync(new GetCalendarQuery(Monday, Monday.AddDays(27)));

        calendar.Stats.BestWeekday.Should().Be(DayOfWeek.Monday);
        calendar.Stats.AdherencePct.Should().Be(100);
    }

    [Fact]
    public async Task AddSession_PutsAnExtraSessionOnTheCalendar()
    {
        await using var app = await TestApp.CreateAsync();
        var goal = await app.CreateGoalAsync();
        var plan = (await app.Dispatcher.SendAsync(Plans.Listening(goal))).ShouldSucceed();
        (await app.Dispatcher.QueryAsync(new GetSchedulablePlansQuery())).Should().ContainSingle(p => p.PlanId == plan.PlanId);

        var id = (await app.Dispatcher.SendAsync(new AddSessionCommand(plan.PlanId, Monday.AddDays(1)))).ShouldSucceed();

        var week = await app.Dispatcher.QueryAsync(new GetCalendarQuery(Monday, Sunday));
        week.Sessions.Single(s => s.OccurrenceId == id).Should().Match<CalendarSessionDto>(s =>
            s.Date == Monday.AddDays(1) && s.Time == new TimeOnly(7, 30));

        (await app.Dispatcher.SendAsync(new AddSessionCommand(plan.PlanId, Monday.AddDays(-1)))).ShouldFailWith("occurrence.past_date");
        await app.Dispatcher.SendAsync(new ArchivePlanCommand(plan.PlanId));
        (await app.Dispatcher.SendAsync(new AddSessionCommand(plan.PlanId, Monday.AddDays(1)))).ShouldFailWith("plan.not_active");
        (await app.Dispatcher.SendAsync(new AddSessionCommand(Guid.NewGuid(), Monday))).ShouldFailWith("plan.not_found");
        (await app.Dispatcher.QueryAsync(new GetSchedulablePlansQuery())).Should().BeEmpty();
    }
}
