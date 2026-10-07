using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Desktop.Views.Dialogs;
using GrowthTracker.Desktop.Views.Pages;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Desktop.Tests;

/// <summary>Sprint 3 acceptance: create Goal → Milestone → Plan and see its sessions scheduled (TDD §10).</summary>
public class GoalsFlowTests
{
    private static async Task<GoalsViewModel> OpenGoalsAsync(TestHost host, MainWindowViewModelAccessor mw)
    {
        mw.Vm.SelectedNavItem = mw.Vm.NavItems.Single(i => i.PageType == typeof(GoalsViewModel));
        await TestHost.WaitForAsync(() => mw.Vm.CurrentPage is GoalsViewModel { IsLoading: false });
        return (GoalsViewModel)mw.Vm.CurrentPage!;
    }

    [AvaloniaFact]
    public async Task EmptyGoals_ShowsEmptyState()
    {
        await using var host = await TestHost.CreateAsync();
        var (window, vm) = host.OpenMainWindow();

        var goals = await OpenGoalsAsync(host, new(vm));

        goals.IsEmpty.Should().BeTrue();
        goals.AreaFilters.Should().HaveCount(7); // "Tất cả" + 6 default Areas
        await TestHost.WaitForAsync(() => window.GetVisualDescendants().OfType<Border>().Any(b => b.Name == "EmptyState" && b.IsEffectivelyVisible));
    }

    [AvaloniaFact]
    public async Task CreateGoal_ThenMilestone_ThenPlan_SchedulesSessions()
    {
        await using var host = await TestHost.CreateAsync();
        var (window, vm) = host.OpenMainWindow();
        var goals = await OpenGoalsAsync(host, new(vm));

        // 1. Create the Goal through the two-step dialog.
        var creating = goals.CreateGoalCommand.ExecuteAsync(null);
        var goalDialog = await host.WaitForDialogAsync<GoalEditorViewModel>();
        await TestHost.WaitForAsync(() => window.GetVisualDescendants().OfType<GoalEditorView>().Any());
        goalDialog.NextCommand.Execute(null);
        goalDialog.ErrorMessage.Should().Be("Hãy nhập tên mục tiêu.");
        goalDialog.Name = "Nâng cao phản xạ tiếng Anh chuyên ngành";
        goalDialog.SelectedArea = goalDialog.Areas.Single(a => a.Name == "English");
        goalDialog.NextCommand.Execute(null);
        goalDialog.IsStepTwo.Should().BeTrue();
        goalDialog.TargetDescription = "IELTS 7.0";
        await goalDialog.SubmitCommand.ExecuteAsync(null);
        await creating;

        // 2. It lands on Goal Detail as a Draft goal.
        await TestHost.WaitForAsync(() => vm.CurrentPage is GoalDetailViewModel { HasDetail: true });
        var detail = (GoalDetailViewModel)vm.CurrentPage!;
        detail.Detail!.Status.Should().Be(GoalStatus.Draft);
        detail.IsTreeEmpty.Should().BeTrue();
        await TestHost.WaitForAsync(() => window.GetVisualDescendants().OfType<GoalDetailView>().Any());
        window.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Name == "GoalTitle" && t.Text == "Nâng cao phản xạ tiếng Anh chuyên ngành");

        // 3. Add a Milestone.
        var addingMilestone = detail.AddMilestoneCommand.ExecuteAsync(null);
        var milestoneDialog = await host.WaitForDialogAsync<MilestoneEditorViewModel>();
        milestoneDialog.Name = "Nghe hiểu & phản xạ tự nhiên";
        await milestoneDialog.SubmitCommand.ExecuteAsync(null);
        await addingMilestone;
        await TestHost.WaitForAsync(() => detail.Milestones.Count == 1);

        // 4. Add a Time plan to that Milestone from the milestone's menu.
        var addingPlan = detail.Milestones[0].AddPlanCommand.ExecuteAsync(null);
        var planDialog = await host.WaitForDialogAsync<PlanEditorViewModel>();
        await TestHost.WaitForAsync(() => window.GetVisualDescendants().OfType<PlanEditorView>().Any());
        planDialog.SelectedMilestone.Id.Should().Be(detail.Milestones[0].Id);
        planDialog.Name = "Listening 20 phút (Tech Pods)";
        planDialog.TargetValue = 20;
        planDialog.PreviewText.Should().StartWith("Sẽ lên lịch 24 buổi trong 8 tuần tới (T2, T4, T6)");
        await planDialog.SubmitCommand.ExecuteAsync(null);
        await addingPlan;

        // 5. The plan shows under its Milestone with sessions scheduled, and the Goal is now Active.
        await TestHost.WaitForAsync(() => detail.Milestones.Count == 1 && detail.Milestones[0].Plans.Count == 1);
        var plan = detail.Milestones[0].Plans[0];
        plan.Dto.UpcomingSessions.Should().Be(24);
        plan.NextSessionText.Should().Be("05/10/2026");
        plan.TargetText.Should().Be("20 phút/buổi");
        detail.Detail!.Status.Should().Be(GoalStatus.Active);
        detail.PlansTabText.Should().Be("Plans (1)");

        // 6. Back on the list, the card reflects the new plan.
        await detail.BackCommand.ExecuteAsync(null);
        await TestHost.WaitForAsync(() => vm.CurrentPage is GoalsViewModel { IsLoading: false, HasGoals: true });
        var list = (GoalsViewModel)vm.CurrentPage!;
        list.Goals.Single().PlanCountText.Should().Be("1 Plan đang chạy");
        list.Summary!.ActivePlans.Should().Be(1);
        await TestHost.WaitForAsync(() => window.GetVisualDescendants().OfType<GoalsView>().Any());
    }

    [AvaloniaFact]
    public async Task CancelledDialog_CreatesNothing()
    {
        await using var host = await TestHost.CreateAsync();
        var (_, vm) = host.OpenMainWindow();
        var goals = await OpenGoalsAsync(host, new(vm));

        var creating = goals.CreateGoalCommand.ExecuteAsync(null);
        var dialog = await host.WaitForDialogAsync<GoalEditorViewModel>();
        vm.CloseDialogCommand.Execute(null); // Escape
        await creating;

        vm.HasDialog.Should().BeFalse();
        vm.CurrentPage.Should().BeOfType<GoalsViewModel>();
        dialog.Completion.Result.Should().BeNull();
    }

    [AvaloniaFact]
    public async Task PlanEditor_SwitchesInputsByType_AndValidates()
    {
        await using var host = await TestHost.CreateAsync();
        var goalId = (await host.UseCases.SendAsync(new Application.Goals.CreateGoalCommand(
            new Application.Goals.GoalFields((await host.UseCases.QueryAsync(new Application.Areas.GetAreasQuery()))[0].Id,
                "G", TestHost.Today, TestHost.Today.AddMonths(3), 2)))).Value;
        var goal = (await host.UseCases.QueryAsync(new Application.Goals.GetGoalDetailQuery(goalId)))!;
        var editor = new PlanEditorViewModel(host.UseCases, goal, null, TestHost.Today);

        editor.ShowsTarget.Should().BeTrue();
        editor.SelectedType = PlanEditorViewModel.PlanTypes.Single(t => t.Value == PlanType.Frequency);
        editor.ShowsFrequency.Should().BeTrue();
        editor.ShowsDayToggles.Should().BeFalse(); // spread across the week automatically
        editor.PreviewText.Should().Contain("T2, T4, T6");

        editor.SelectedType = PlanEditorViewModel.PlanTypes.Single(t => t.Value == PlanType.Achievement);
        editor.Name = "IELTS";
        await editor.SubmitCommand.ExecuteAsync(null);
        editor.ErrorMessage.Should().Be("Thêm ít nhất một tiêu chí hoàn thành.");

        editor.NewCriterion = "Đăng ký thi";
        editor.AddCriterionCommand.Execute(null);
        editor.Criteria.Should().Equal("Đăng ký thi");
        foreach (var toggle in editor.DayToggles)
            toggle.IsChecked = false;
        await editor.SubmitCommand.ExecuteAsync(null);
        editor.ErrorMessage.Should().Be("Chọn ít nhất một ngày trong tuần.");

        editor.DayToggles[5].IsChecked = true; // Saturday
        await editor.SubmitCommand.ExecuteAsync(null);
        editor.ErrorMessage.Should().BeNull();
        editor.Completion.Result!.SessionsScheduled.Should().Be(8);
    }

    [AvaloniaFact]
    public async Task ArchivePlan_AsksForConfirmation()
    {
        await using var host = await TestHost.CreateAsync();
        var (_, vm) = host.OpenMainWindow();
        var areaId = (await host.UseCases.QueryAsync(new Application.Areas.GetAreasQuery()))[0].Id;
        var goalId = (await host.UseCases.SendAsync(new Application.Goals.CreateGoalCommand(
            new Application.Goals.GoalFields(areaId, "G", TestHost.Today, TestHost.Today.AddMonths(3), 2)))).Value;
        await host.UseCases.SendAsync(new Application.Plans.CreatePlanCommand(goalId, "Read", PlanType.Binary, 1, null,
            TestHost.Today, TestHost.Today.AddDays(13), Difficulty.Easy));

        await host.Navigation.NavigateAsync<GoalDetailViewModel>(goalId);
        var detail = (GoalDetailViewModel)vm.CurrentPage!;
        await TestHost.WaitForAsync(() => detail.UnassignedPlans.Count == 1);

        var archiving = detail.UnassignedPlans[0].ArchiveCommand.ExecuteAsync(null);
        var confirm = await host.WaitForDialogAsync<ConfirmDialogViewModel>();
        confirm.ConfirmCommand.Execute(null);
        await archiving;

        await TestHost.WaitForAsync(() => detail.UnassignedPlans.Count == 1 && !detail.UnassignedPlans[0].IsActive);
        detail.UnassignedPlans[0].Dto.UpcomingSessions.Should().Be(0);
    }

    [AvaloniaFact]
    public async Task Settings_AddArea_AppearsInList()
    {
        await using var host = await TestHost.CreateAsync();
        var (_, vm) = host.OpenMainWindow();
        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(SettingsViewModel));
        await TestHost.WaitForAsync(() => vm.CurrentPage is SettingsViewModel { Areas.Count: 6 });
        var settings = (SettingsViewModel)vm.CurrentPage!;

        var adding = settings.AddAreaCommand.ExecuteAsync(null);
        var dialog = await host.WaitForDialogAsync<AreaEditorViewModel>();
        dialog.Name = "Side Projects";
        dialog.SelectCommand.Execute(dialog.Swatches[6]);
        await dialog.SubmitCommand.ExecuteAsync(null);
        await adding;

        await TestHost.WaitForAsync(() => settings.Areas.Count == 7);
        settings.Areas.Should().Contain(a => a.Name == "Side Projects");
    }
}

/// <summary>Small wrapper so helpers can take the window ViewModel without a tuple.</summary>
public sealed record MainWindowViewModelAccessor(ViewModels.MainWindowViewModel Vm);
