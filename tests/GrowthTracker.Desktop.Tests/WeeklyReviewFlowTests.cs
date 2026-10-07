using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Jobs;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Desktop.Views.Pages;
using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Desktop.Tests;

/// <summary>Sprint 6a: the weekly loop Review → Adjust works from the UI (FR-40 → FR-43, FR-52 → FR-54).</summary>
public class WeeklyReviewFlowTests
{
    private static readonly DateOnly Week4 = TestHost.Today.AddDays(21);

    /// <summary>Four weeks: Gym (5/week) done only Mon/Tue, Speaking (20 min M/W/F) always done; ends Sunday 21:00 of week 4.</summary>
    private static async Task<(Guid GoalId, Guid Gym)> FourWeeksAsync(TestHost host)
    {
        var english = (await host.UseCases.QueryAsync(new GetAreasQuery())).Single(a => a.Name == "English").Id;
        var goal = (await host.UseCases.SendAsync(new CreateGoalCommand(
            new GoalFields(english, "Improve English", TestHost.Today, TestHost.Today.AddDays(120), 2)))).Value;
        var end = TestHost.Today.AddDays(90);
        var gym = (await host.UseCases.SendAsync(new CreatePlanCommand(goal, "Gym", PlanType.Frequency, 5, "buổi", TestHost.Today, end,
            Difficulty.Hard, WeekDays.Weekdays, FrequencyPerWeek: 5))).Value!.PlanId;
        var speaking = (await host.UseCases.SendAsync(new CreatePlanCommand(goal, "Speaking", PlanType.Time, 20, "phút", TestHost.Today, end,
            Difficulty.Normal, WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday))).Value!.PlanId;
        for (int day = 0; day < 28; day++)
        {
            var date = TestHost.Today.AddDays(day);
            if (date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Tuesday)
                await host.LogSessionAsync(gym, date, new LogEntry(1, true, 3, null));
            if (date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday)
                await host.LogSessionAsync(speaking, date, new LogEntry(20, true, 5, null));
        }
        host.Clock.UtcNow = Week4.AddDays(6).ToDateTime(new TimeOnly(21, 0), DateTimeKind.Utc);
        await host.UseCases.SendAsync(new RunWeeklyReviewJobCommand());
        return (goal, gym);
    }

    private static async Task<WeeklyReviewViewModel> OpenReviewAsync(TestHost host)
    {
        var (_, vm) = host.OpenMainWindow();
        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(WeeklyReviewViewModel));
        await TestHost.WaitForAsync(() => vm.CurrentPage is WeeklyReviewViewModel { IsLoading: false, Data: not null });
        return (WeeklyReviewViewModel)vm.CurrentPage!;
    }

    [AvaloniaFact]
    public async Task EmptyWeek_ExplainsWhyThereAreNoSuggestions()
    {
        await using var host = await TestHost.CreateAsync();

        var review = await OpenReviewAsync(host);

        review.Data!.WeekStart.Should().Be(TestHost.Today.AddDays(-7)); // Monday: last week by default
        review.StatusText.Should().Be("Chưa review");
        review.ScoreText.Should().Be("—");
        review.SuggestionHint.Should().StartWith("Cần thêm dữ liệu");
        review.CanGoNext.Should().BeTrue();
        await TestHost.WaitForAsync(() => host.Window!.GetVisualDescendants().OfType<WeeklyReviewView>().Any());
    }

    [AvaloniaFact]
    public async Task FourWeeks_ApplyDismissAndComplete()
    {
        await using var host = await TestHost.CreateAsync();
        var (_, gym) = await FourWeeksAsync(host);
        var review = await OpenReviewAsync(host); // Sunday: this week

        review.Data!.WeekStart.Should().Be(Week4);
        review.WeekText.Should().Be("Tuần 44");
        review.StatusText.Should().Be("Tạm tính");
        review.DoneText.Should().Be("5 / 8 buổi");
        review.Areas.Single().Name.Should().Be("English");
        review.Attention.Should().Contain(a => a.Title == "Gym có thể quá khó");
        review.Suggestions.Should().HaveCount(2);
        var gymSuggestion = review.Suggestions.Single(s => s.PlanName == "Gym");
        gymSuggestion.Text.Should().Be("Giảm từ 5 xuống 2 buổi/tuần có thể phù hợp hơn.");

        // Apply asks for confirmation first.
        var applying = gymSuggestion.ApplyCommand.ExecuteAsync(null);
        var confirm = await host.WaitForDialogAsync<ConfirmDialogViewModel>();
        confirm.Message.Should().Contain("từ ngày mai");
        confirm.ConfirmCommand.Execute(null);
        await applying;
        await TestHost.WaitForAsync(() => review.Suggestions.Single(s => s.PlanName == "Gym").StateText == "Đã áp dụng");

        await review.Suggestions.Single(s => s.PlanName == "Speaking").DismissCommand.ExecuteAsync(null);
        await TestHost.WaitForAsync(() => review.Suggestions.Single(s => s.PlanName == "Speaking").StateText == "Đã bỏ qua 4 tuần");

        review.Note = "Gym quá dày, giảm lại.";
        await review.CompleteCommand.ExecuteAsync(null);
        review.IsCompleted.Should().BeTrue();
        review.StatusText.Should().Be("Đã hoàn thành");
        review.CompleteText.Should().Be("Đã hoàn tất review tuần 44");
        review.Note.Should().Be("Gym quá dày, giảm lại.");
    }

    [AvaloniaFact]
    public async Task WeekNavigation()
    {
        await using var host = await TestHost.CreateAsync();
        await FourWeeksAsync(host);
        var review = await OpenReviewAsync(host);

        review.CanGoNext.Should().BeFalse();
        await review.PreviousWeekCommand.ExecuteAsync(null);
        review.Data!.WeekStart.Should().Be(Week4.AddDays(-7));
        review.StatusText.Should().Be("Chưa review"); // the job generates only the latest finished week (week 4)
        review.CanGoNext.Should().BeTrue();
        await review.NextWeekCommand.ExecuteAsync(null);
        review.Data!.WeekStart.Should().Be(Week4);
    }

    [AvaloniaFact]
    public async Task DashboardReminder_OpensTheReview()
    {
        await using var host = await TestHost.CreateAsync();
        await FourWeeksAsync(host);
        host.Clock.UtcNow = host.Clock.UtcNow.AddDays(1); // Monday
        var (_, vm) = host.OpenMainWindow();
        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(DashboardViewModel));
        await TestHost.WaitForAsync(() => vm.CurrentPage is DashboardViewModel { IsLoading: false, Data: not null });
        var dashboard = (DashboardViewModel)vm.CurrentPage!;

        dashboard.HasReviewReminder.Should().BeTrue();
        dashboard.ReviewReminderText.Should().StartWith("Weekly Review tuần 44 đã sẵn sàng");
        await dashboard.OpenReviewCommand.ExecuteAsync(null);

        await TestHost.WaitForAsync(() => vm.CurrentPage is WeeklyReviewViewModel { IsLoading: false, Data: not null });
        ((WeeklyReviewViewModel)vm.CurrentPage!).Data!.WeekStart.Should().Be(Week4);
    }

    [AvaloniaFact]
    public async Task GoalDetail_ShowsTooHardWarning()
    {
        await using var host = await TestHost.CreateAsync();
        var (goal, gym) = await FourWeeksAsync(host);
        host.OpenMainWindow();

        await host.Navigation.NavigateAsync<GoalDetailViewModel>(goal);
        var detail = (GoalDetailViewModel)host.Navigation.Current!;
        await TestHost.WaitForAsync(() => detail.UnassignedPlans.Count == 2);

        var plan = detail.UnassignedPlans.Single(p => p.Id == gym);
        plan.MayBeTooHard.Should().BeTrue();
        plan.TooHardText.Should().StartWith("Có thể quá khó: chỉ đạt 40%");
    }
}
