using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Plans;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Desktop.Views.Pages;
using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Desktop.Tests;

/// <summary>Sprint 5a acceptance: rescheduling from the calendar updates the Reschedule Rate (TDD §10).</summary>
public class CalendarFlowTests
{
    private static async Task SeedAsync(TestHost host)
    {
        var english = (await host.UseCases.QueryAsync(new GetAreasQuery())).Single(a => a.Name == "English").Id;
        var goal = (await host.UseCases.SendAsync(new CreateGoalCommand(
            new GoalFields(english, "Improve English", TestHost.Today, TestHost.Today.AddDays(20), 2)))).Value;
        await host.UseCases.SendAsync(new AddMilestoneCommand(goal, "Vocabulary", TestHost.Today.AddDays(3)));
        await host.UseCases.SendAsync(new CreatePlanCommand(goal, "Listening", PlanType.Time, 20, "phút",
            TestHost.Today, TestHost.Today.AddMonths(2), Difficulty.Normal, WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday,
            PreferredTime: new TimeOnly(7, 30)));
    }

    private static async Task<CalendarViewModel> OpenCalendarAsync(TestHost host)
    {
        var (_, vm) = host.OpenMainWindow();
        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(CalendarViewModel));
        await TestHost.WaitForAsync(() => vm.CurrentPage is CalendarViewModel { IsLoading: false, Data: not null });
        return (CalendarViewModel)vm.CurrentPage!;
    }

    [AvaloniaFact]
    public async Task Month_ShowsSixWeeks_ChipsDeadlinesAndSelectedToday()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);

        var calendar = await OpenCalendarAsync(host);

        calendar.HeaderText.Should().Be("Tháng 10, 2026");
        calendar.Days.Should().HaveCount(42);
        calendar.Days[0].Date.Should().Be(new DateOnly(2026, 9, 28)); // the Monday before Oct 1
        var today = calendar.Days.Single(d => d.IsToday);
        today.IsSelected.Should().BeTrue();
        today.VisibleSessions.Single().Name.Should().Be("Listening");
        calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(3)).Deadlines.Single().Text.Should().Be("Milestone: Vocabulary");
        calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(6)).IsReviewDay.Should().BeTrue();
        calendar.SelectedSessions.Should().ContainSingle();
        calendar.AreaFilters.Select(a => a.Label).Should().Contain("English (" + calendar.Data!.Areas.Single().SessionCount + ")");
        await TestHost.WaitForAsync(() => host.Window!.GetVisualDescendants().OfType<CalendarView>().Any());
    }

    [AvaloniaFact]
    public async Task Week_PositionsTimedBlocksByHour()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var calendar = await OpenCalendarAsync(host);

        await calendar.SetModeCommand.ExecuteAsync(CalendarMode.Week);

        calendar.HeaderText.Should().Be("Tuần 41 · 05/10 – 11/10/2026");
        calendar.WeekColumns.Should().HaveCount(7);
        var block = calendar.WeekColumns[0].Blocks.Single();
        block.Top.Should().Be(72);       // 07:30 is 1.5 h after 06:00, 48 px per hour
        block.Height.Should().Be(22);    // 20 minutes, but at least 22 px to stay readable
    }

    [AvaloniaFact]
    public async Task Navigation_PreviousNextAndToday()
    {
        await using var host = await TestHost.CreateAsync();
        var calendar = await OpenCalendarAsync(host);

        await calendar.NextCommand.ExecuteAsync(null);
        calendar.HeaderText.Should().Be("Tháng 11, 2026");
        await calendar.PreviousCommand.ExecuteAsync(null);
        await calendar.PreviousCommand.ExecuteAsync(null);
        calendar.HeaderText.Should().Be("Tháng 9, 2026");
        await calendar.GoToTodayCommand.ExecuteAsync(null);
        calendar.HeaderText.Should().Be("Tháng 10, 2026");

        await calendar.SetModeCommand.ExecuteAsync(CalendarMode.List);
        calendar.ListDays.Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task DropOnAnotherDay_AsksScope_ThenReschedules_AndUpdatesRate()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var calendar = await OpenCalendarAsync(host);
        var wednesday = calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(2)).Sessions.Single();

        var moving = calendar.MoveSessionAsync(wednesday.OccurrenceId, TestHost.Today.AddDays(3));
        var dialog = await host.WaitForDialogAsync<RescheduleDialogViewModel>();
        dialog.NewDate.Should().Be(TestHost.Today.AddDays(3).ToDateTime(TimeOnly.MinValue));
        dialog.MoveFollowing.Should().BeFalse();
        dialog.Reason = "Bận họp";
        await dialog.SubmitCommand.ExecuteAsync(null);
        await moving;

        await TestHost.WaitForAsync(() => calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(3)).Sessions.Count == 1);
        var moved = calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(3)).Sessions.Single();
        moved.IsMoved.Should().BeTrue();
        moved.StateText.Should().Be("Dời từ 07/10/2026");
        calendar.SelectedDate.Should().Be(TestHost.Today.AddDays(3));
        calendar.Data!.Stats.RescheduleRatePct.Should().BeGreaterThan(0);
        calendar.RescheduleText.Should().StartWith("Tỷ lệ dời lịch");
    }

    [AvaloniaFact]
    public async Task DropOnThePast_IsRefused()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var calendar = await OpenCalendarAsync(host);
        var monday = calendar.Days.Single(d => d.IsToday).Sessions.Single();

        await calendar.MoveSessionAsync(monday.OccurrenceId, TestHost.Today.AddDays(-1));

        calendar.ActionError.Should().Be("Không thể xếp lịch vào ngày đã qua.");
        host.Dialogs.Current.Should().BeNull();
    }

    [AvaloniaFact]
    public async Task RescheduleButton_FollowingSessions_MovesTheRest()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var calendar = await OpenCalendarAsync(host);
        var wednesday = calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(2));
        await wednesday.SelectCommand.ExecuteAsync(null);

        var moving = calendar.SelectedSessions.Single().RescheduleCommand.ExecuteAsync(null);
        var dialog = await host.WaitForDialogAsync<RescheduleDialogViewModel>();
        dialog.NewDate = TestHost.Today.AddDays(3).ToDateTime(TimeOnly.MinValue);
        dialog.SetScopeCommand.Execute(true);
        await dialog.SubmitCommand.ExecuteAsync(null);
        await moving;

        (await dialog.Completion).Should().BeGreaterThan(1);
        await TestHost.WaitForAsync(() => calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(5)).Sessions.Count == 1);
        calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(4)).Sessions.Should().BeEmpty(); // Friday moved to Saturday
    }

    [AvaloniaFact]
    public async Task AddSession_FromCalendar()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var calendar = await OpenCalendarAsync(host);
        await calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(1)).SelectCommand.ExecuteAsync(null);

        var adding = calendar.AddSessionCommand.ExecuteAsync(null);
        var dialog = await host.WaitForDialogAsync<AddSessionDialogViewModel>();
        dialog.SelectedPlan!.Name.Should().Be("Listening");
        dialog.Time.Should().Be(new TimeSpan(7, 30, 0));
        dialog.Date.Should().Be(TestHost.Today.AddDays(1).ToDateTime(TimeOnly.MinValue));
        await dialog.SubmitCommand.ExecuteAsync(null);
        await adding;

        await TestHost.WaitForAsync(() => calendar.Days.Single(d => d.Date == TestHost.Today.AddDays(1)).Sessions.Count == 1);
    }
}
