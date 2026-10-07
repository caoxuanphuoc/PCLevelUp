using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Plans;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Desktop.Views.Dialogs;
using GrowthTracker.Desktop.Views.Pages;
using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Desktop.Tests;

/// <summary>Sprint 4a acceptance: log a session from Today and see Score and XP right away (TDD §10).</summary>
public class TodayFlowTests
{
    /// <summary>One English goal with a 07:30 Listening plan (20 min, Mon/Wed/Fri) and an any-time daily Binary plan.</summary>
    private static async Task SeedAsync(TestHost host)
    {
        var english = (await host.UseCases.QueryAsync(new GetAreasQuery())).Single(a => a.Name == "English").Id;
        var goal = (await host.UseCases.SendAsync(new CreateGoalCommand(
            new GoalFields(english, "Improve English", TestHost.Today, TestHost.Today.AddMonths(6), 2)))).Value;
        await host.UseCases.SendAsync(new CreatePlanCommand(goal, "Listening (Tech Pods)", PlanType.Time, 20, "phút",
            TestHost.Today, TestHost.Today.AddMonths(3), Difficulty.Normal, WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday,
            PreferredTime: new TimeOnly(7, 30)));
        await host.UseCases.SendAsync(new CreatePlanCommand(goal, "Shadowing", PlanType.Binary, 1, null,
            TestHost.Today, TestHost.Today.AddMonths(1), Difficulty.Easy));
    }

    private static async Task<TodayViewModel> OpenTodayAsync(TestHost host)
    {
        var (_, vm) = host.OpenMainWindow();
        await TestHost.WaitForAsync(() => vm.CurrentPage is TodayViewModel { IsLoading: false, Data: not null });
        return (TodayViewModel)vm.CurrentPage!;
    }

    [AvaloniaFact]
    public async Task NoPlans_ShowsEmptyState()
    {
        await using var host = await TestHost.CreateAsync();

        var today = await OpenTodayAsync(host);

        today.IsEmpty.Should().BeTrue();
        today.DateText.Should().Be("Thứ Hai, 05/10/2026");
        today.WeekText.Should().Be("Tuần 41 / 2026");
    }

    [AvaloniaFact]
    public async Task Sessions_AreGroupedByTimeOfDay()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);

        var today = await OpenTodayAsync(host);

        today.Groups.Select(g => g.Title).Should().Equal("SÁNG (1)", "BẤT KỲ LÚC NÀO (1)");
        today.Groups[0].Items.Single().TargetText.Should().Be("Mục tiêu: 20 phút/buổi");
        today.Groups[1].Items.Single().CanQuickTick.Should().BeTrue();
        today.DayCountText.Should().Be("(0/2 hoàn thành)");
    }

    [AvaloniaFact]
    public async Task QuickTick_LogsBinaryPlan_InOneClick()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var today = await OpenTodayAsync(host);

        await today.Groups[1].Items.Single().QuickDoneCommand.ExecuteAsync(null);

        await TestHost.WaitForAsync(() => today.Groups[0].Title == "ĐÃ GHI NHẬN (1)");
        var logged = today.Groups[0].Items.Single();
        logged.IsDone.Should().BeTrue();
        logged.ScoreText.Should().Be("94"); // C 100 · Q 80 (4★) · S 100 → 45 + 24 + 25
        logged.SavedText.Should().Be("Đã lưu 09:00 · Nhấn để sửa (còn 24h)");
        today.DayProgressText.Should().Be("50%");
        today.StreakText.Should().Be("1 ngày streak");
    }

    [AvaloniaFact]
    public async Task LogThroughDialog_ShowsScoreBreakdownAndXp_ThenEditWithin24h()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var today = await OpenTodayAsync(host);
        var listening = today.Groups[0].Items.Single();

        // Step 1: form, Actual defaults to Target.
        var opening = listening.OpenCommand.ExecuteAsync(null);
        var dialog = await host.WaitForDialogAsync<PlanResultViewModel>();
        await TestHost.WaitForAsync(() => host.Window!.GetVisualDescendants().OfType<PlanResultView>().Any());
        dialog.IsFormStep.Should().BeTrue();
        dialog.UsesAmount.Should().BeTrue();
        dialog.Actual.Should().Be(20);
        dialog.Quality.Should().Be(4);
        dialog.IncreaseCommand.Execute(null);
        dialog.Actual.Should().Be(25);
        dialog.Note = "Tập trung tốt";
        await dialog.SubmitCommand.ExecuteAsync(null);

        // Step 2: Score, breakdown with the V2 weights, XP and Area level.
        dialog.IsResultStep.Should().BeTrue();
        dialog.Score.Should().Be(94);
        dialog.ScoreLabel.Should().Be("XUẤT SẮC");
        dialog.XpText.Should().Be("+98,7 XP");
        dialog.Breakdown.Select(b => b.Title).Should().Equal(
            "Hoàn thành (45%)", "Chất lượng (30%)", "Nhất quán (25%)", "Độ khó (hệ số XP)");
        dialog.Breakdown[0].Detail.Should().Be("Thực tế 25 / 20 phút");
        dialog.AreaLevelText.Should().Be("English: Level 1 · 98,7 XP");
        dialog.NextLevelText.Should().Be("Còn 51,3 XP lên Level 2");

        // Edit within 24h: back to the form, 5 stars → Score 100.
        dialog.CanEdit.Should().BeTrue();
        dialog.EditAgainCommand.Execute(null);
        dialog.IsFormStep.Should().BeTrue();
        dialog.Quality = 5;
        await dialog.SubmitCommand.ExecuteAsync(null);
        dialog.Score.Should().Be(100);
        dialog.FinishCommand.Execute(null);
        await opening;

        await TestHost.WaitForAsync(() => today.Groups[0].Title == "ĐÃ GHI NHẬN (1)");
        today.Groups[0].Items.Single().ScoreText.Should().Be("100");
        today.Data!.XpToday.Should().Be(105m);
    }

    [AvaloniaFact]
    public async Task ReopeningLoggedSession_StartsOnResult()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var today = await OpenTodayAsync(host);
        await today.Groups[1].Items.Single().QuickDoneCommand.ExecuteAsync(null);
        await TestHost.WaitForAsync(() => today.Groups[0].Title == "ĐÃ GHI NHẬN (1)");

        var opening = today.Groups[0].Items.Single().OpenCommand.ExecuteAsync(null);
        var dialog = await host.WaitForDialogAsync<PlanResultViewModel>();

        dialog.IsResultStep.Should().BeTrue();
        dialog.UsesDoneToggle.Should().BeTrue();
        dialog.Done.Should().BeTrue();
        dialog.CancelCommand.Execute(null); // closing still reports the saved result
        (await dialog.Completion).Should().NotBeNull();
        await opening;
    }

    [AvaloniaFact]
    public async Task Skip_MovesSessionToCollapsedSkippedGroup()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var today = await OpenTodayAsync(host);

        await today.Groups[0].Items.Single().SkipCommand.ExecuteAsync(SkipReason.Sick);

        await TestHost.WaitForAsync(() => today.Groups.Any(g => g.Title == "ĐÃ BỎ QUA (1)"));
        var skipped = today.Groups.Single(g => g.Title == "ĐÃ BỎ QUA (1)");
        skipped.IsExpanded.Should().BeFalse();
        skipped.Items.Single().SkipText.Should().Be("Lý do: Ốm");
        today.DayCountText.Should().Be("(0/1 hoàn thành)"); // skipped sessions leave the day's goal
    }

    [AvaloniaFact]
    public async Task Timer_FillsActualWithElapsedMinutes()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var today = await OpenTodayAsync(host);
        var opening = today.Groups[0].Items.Single().OpenCommand.ExecuteAsync(null);
        var dialog = await host.WaitForDialogAsync<PlanResultViewModel>();

        dialog.ToggleTimerCommand.Execute(null);
        dialog.IsTimerRunning.Should().BeTrue();
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(11).AddSeconds(20);
        dialog.ToggleTimerCommand.Execute(null);

        dialog.IsTimerRunning.Should().BeFalse();
        dialog.Actual.Should().Be(12);
        dialog.CancelCommand.Execute(null);
        (await dialog.Completion).Should().BeNull(); // nothing saved
        await opening;
    }

    [AvaloniaFact]
    public async Task TodayView_RendersSessionsAndWeekStrip()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var today = await OpenTodayAsync(host);
        var window = host.Window!;

        await TestHost.WaitForAsync(() => window.GetVisualDescendants().OfType<TodayView>().Any());
        today.Week.Should().HaveCount(7);
        today.Week[0].IsToday.Should().BeTrue();
        window.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Name == "DateTitle" && t.Text == "Thứ Hai, 05/10/2026");
    }
}
