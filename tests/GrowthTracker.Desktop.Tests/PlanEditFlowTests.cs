using Avalonia.Headless.XUnit;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Desktop.Tests;

/// <summary>"Đã kết thúc" on Goal Detail, overdue milestones, and "Sửa kế hoạch" from the plan menu.</summary>
public class PlanEditFlowTests
{
    /// <summary>A Goal with a milestone due on Thursday and a daily "Read" plan for this week only; the clock ends next Tuesday.</summary>
    private static async Task<(Guid Goal, Guid Plan)> EndedPlanAsync(TestHost host)
    {
        var english = (await host.UseCases.QueryAsync(new GetAreasQuery())).Single(a => a.Name == "English").Id;
        var goal = (await host.UseCases.SendAsync(new CreateGoalCommand(
            new GoalFields(english, "English Input", TestHost.Today, TestHost.Today.AddDays(60), 1)))).Value;
        var milestone = (await host.UseCases.SendAsync(new AddMilestoneCommand(goal, "Nghe tiếng anh", TestHost.Today.AddDays(3)))).Value;
        var plan = (await host.UseCases.SendAsync(new CreatePlanCommand(goal, "Read", PlanType.Binary, 1, null, TestHost.Today,
            TestHost.Today.AddDays(6), Difficulty.Easy, WeekDays.All, PreferredTime: new TimeOnly(21, 0), MilestoneId: milestone))).Value!.PlanId;
        await host.LogSessionAsync(plan, TestHost.Today, new LogEntry(1, true, 4, null));
        host.Clock.UtcNow = TestHost.Today.AddDays(8).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        return (goal, plan);
    }

    private static async Task<GoalDetailViewModel> OpenGoalAsync(TestHost host, Guid goal)
    {
        host.OpenMainWindow();
        await host.Navigation.NavigateAsync<GoalDetailViewModel>(goal);
        var detail = (GoalDetailViewModel)host.Navigation.Current!;
        await TestHost.WaitForAsync(() => detail is { IsLoading: false, Detail: not null });
        return detail;
    }

    [AvaloniaFact]
    public async Task EndedPlan_AndOverdueMilestone_AreLabelled()
    {
        await using var host = await TestHost.CreateAsync();
        var (goal, _) = await EndedPlanAsync(host);

        var detail = await OpenGoalAsync(host, goal);

        var milestone = detail.Milestones.Single();
        milestone.IsOverdue.Should().BeTrue();
        milestone.StatusText.Should().StartWith("Quá hạn");
        var plan = milestone.Plans.Single();
        plan.StatusLabel.Should().Be("Đã kết thúc");
        plan.IsRunning.Should().BeFalse(); // dimmed
        plan.IsActive.Should().BeTrue();   // the menu stays, so it can be extended
        plan.UpcomingText.Should().Be("Kết thúc 11/10/2026");
    }

    [AvaloniaFact]
    public async Task EditPlan_FromTheMenu_ExtendsTheEndDate()
    {
        await using var host = await TestHost.CreateAsync();
        var (goal, _) = await EndedPlanAsync(host);
        var detail = await OpenGoalAsync(host, goal);

        var editing = detail.Milestones.Single().Plans.Single().EditCommand.ExecuteAsync(null);
        var editor = await host.WaitForDialogAsync<PlanEditorViewModel>();

        editor.IsEditing.Should().BeTrue();
        (editor.Title, editor.SubmitText).Should().Be(("Sửa kế hoạch", "Lưu thay đổi"));
        editor.CanChangeTypeAndStart.Should().BeFalse();
        editor.Name.Should().Be("Read");
        editor.SelectedType.Value.Should().Be(PlanType.Binary);
        editor.DayToggles.Should().OnlyContain(t => t.IsChecked);
        editor.PreferredTime.Should().Be(new TimeSpan(21, 0, 0));
        editor.SelectedMilestone.Name.Should().Be("Nghe tiếng anh");

        editor.DurationEnd = TestHost.Today.AddDays(7).ToDateTime(TimeOnly.MinValue); // yesterday
        await editor.SubmitCommand.ExecuteAsync(null);
        editor.ErrorMessage.Should().Be("Ngày kết thúc mới không được trước hôm nay.");

        editor.DurationEnd = TestHost.Today.AddDays(20).ToDateTime(TimeOnly.MinValue);
        editor.PreviewText.Should().Contain("Tổng 21 buổi");
        await editor.SubmitCommand.ExecuteAsync(null);
        editor.ErrorMessage.Should().BeNull();
        await editing;

        await TestHost.WaitForAsync(() => detail is { IsLoading: false } && detail.Milestones.Single().Plans.Single().StatusLabel == "Đang chạy");
        var plan = detail.Milestones.Single().Plans.Single();
        plan.NextSessionText.Should().Be("13/10/2026"); // today
        plan.UpcomingText.Should().Be("13 buổi sắp tới");
    }
}
