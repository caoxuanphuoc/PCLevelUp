using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Dashboard;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Jobs;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Desktop.ViewModels.Pages;
using GrowthTracker.Desktop.Views.Pages;
using GrowthTracker.Domain.Plans;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop.Tests;

/// <summary>Sprint 5b acceptance: the Dashboard matches the data (TDD §10).</summary>
public class DashboardFlowTests
{
    private static async Task<(Guid GoalId, Guid PlanId)> SeedAsync(TestHost host, int loggedDays = 1)
    {
        var english = (await host.UseCases.QueryAsync(new GetAreasQuery())).Single(a => a.Name == "English").Id;
        var goal = (await host.UseCases.SendAsync(new CreateGoalCommand(
            new GoalFields(english, "Improve English", TestHost.Today, TestHost.Today.AddDays(60), 2)))).Value;
        var plan = (await host.UseCases.SendAsync(new CreatePlanCommand(goal, "Shadowing", PlanType.Binary, 1, null,
            TestHost.Today, TestHost.Today.AddDays(59), Difficulty.Easy))).Value!.PlanId;
        for (int day = 0; day < loggedDays; day++)
        {
            var date = TestHost.Today.AddDays(day);
            host.Clock.UtcNow = date.ToDateTime(new TimeOnly(20, 0), DateTimeKind.Utc);
            var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
            var session = await db.PlanOccurrences.Where(o => o.PlanId == plan && o.ScheduledDate == date).Select(o => o.Id).SingleAsync();
            scope.Dispose();
            await host.UseCases.SendAsync(new LogPlanResultCommand(session, new LogEntry(1, true, 4, null)));
        }
        return (goal, plan);
    }

    private static async Task<DashboardViewModel> OpenDashboardAsync(TestHost host)
    {
        var (_, vm) = host.OpenMainWindow();
        vm.SelectedNavItem = vm.NavItems.Single(i => i.PageType == typeof(DashboardViewModel));
        await TestHost.WaitForAsync(() => vm.CurrentPage is DashboardViewModel { IsLoading: false, Data: not null });
        return (DashboardViewModel)vm.CurrentPage!;
    }

    [AvaloniaFact]
    public async Task NewUser_SeesGettingStartedSteps()
    {
        await using var host = await TestHost.CreateAsync();

        var dashboard = await OpenDashboardAsync(host);

        dashboard.IsNewUser.Should().BeTrue();
        dashboard.HasData.Should().BeFalse();
        dashboard.StepGoalDone.Should().BeFalse();
        dashboard.Greeting.Should().Be("Chào buổi sáng"); // 09:00
        await TestHost.WaitForAsync(() => host.Window!.GetVisualDescendants().OfType<DashboardView>().Any());
    }

    [AvaloniaFact]
    public async Task WithData_ShowsScoreAreaCardsAndToday()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);

        var dashboard = await OpenDashboardAsync(host);

        dashboard.HasData.Should().BeTrue();
        dashboard.ScoreText.Should().Be("94"); // one Easy Binary log with 4 stars
        dashboard.LevelText.Should().Be("Level 1");
        dashboard.StreakText.Should().Be("Streak 1 ngày");
        dashboard.CollectingData.Should().BeTrue();
        dashboard.ShowTrendChart.Should().BeFalse();
        dashboard.TrendLabel.Should().Be("Đang thu thập dữ liệu");
        var english = dashboard.Areas.Should().ContainSingle().Which;
        english.Name.Should().Be("English");
        english.ScoreText.Should().Be("94");
        dashboard.TodaySessions.Should().ContainSingle().Which.IsLogged.Should().BeTrue();
        dashboard.TodayTitle.Should().Be("HÔM NAY (1/1)");
        dashboard.TodaySubtitle.Should().Be("Bạn đã hoàn thành cả 1 kế hoạch hôm nay.");
    }

    [AvaloniaFact]
    public async Task AfterAWeek_TrendChartIsDrawnWithLiveCharts()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host, loggedDays: 8);

        var dashboard = await OpenDashboardAsync(host);

        dashboard.ShowTrendChart.Should().BeTrue();
        var series = dashboard.TrendSeries.Should().ContainSingle().Which.Should().BeOfType<LineSeries<ObservableValue>>().Which;
        series.Values!.Select(v => v.Value).Should().Equal(null, null, null, null, 94d, 94d);
        dashboard.TrendXAxes[0].Labels.Should().HaveCount(6).And.EndWith("T42");
        await TestHost.WaitForAsync(() => host.Window!.GetVisualDescendants().OfType<LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart>().Any());
    }

    [AvaloniaFact]
    public async Task AreaCard_OpensGoalsFilteredToThatArea()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var dashboard = await OpenDashboardAsync(host);

        await dashboard.Areas.Single().OpenCommand.ExecuteAsync(null);

        var goals = (GoalsViewModel)host.Navigation.Current!;
        await TestHost.WaitForAsync(() => goals is { IsLoading: false, HasGoals: true });
        goals.AreaFilters.Single(a => a.IsSelected).Label.Should().StartWith("English");
    }

    [AvaloniaFact]
    public async Task PeriodFilter_ReloadsForMonth()
    {
        await using var host = await TestHost.CreateAsync();
        await SeedAsync(host);
        var dashboard = await OpenDashboardAsync(host);

        dashboard.SelectedPeriod = DashboardViewModel.Periods.Single(p => p.Value == DashboardPeriod.Month);

        await TestHost.WaitForAsync(() => dashboard.Data!.Period == DashboardPeriod.Month);
        dashboard.Data!.PeriodStart.Should().Be(new DateOnly(2026, 10, 1));
    }

    [AvaloniaFact]
    public async Task RiskBanner_CanBeSnoozed()
    {
        await using var host = await TestHost.CreateAsync();
        var english = (await host.UseCases.QueryAsync(new GetAreasQuery())).Single(a => a.Name == "English").Id;
        var goal = (await host.UseCases.SendAsync(new CreateGoalCommand(
            new GoalFields(english, "Improve English", TestHost.Today, TestHost.Today.AddDays(20), 2)))).Value;
        await host.UseCases.SendAsync(new CreatePlanCommand(goal, "Daily", PlanType.Binary, 1, null,
            TestHost.Today, TestHost.Today.AddDays(19), Difficulty.Easy));
        host.Clock.UtcNow = host.Clock.UtcNow.AddDays(8);
        await host.UseCases.SendAsync(new RunRiskScanCommand());
        var dashboard = await OpenDashboardAsync(host);

        dashboard.ShowRiskBanner.Should().BeTrue();
        dashboard.RiskText.Should().Contain("Improve English").And.Contain("kỳ vọng 40%");

        await dashboard.SnoozeRiskCommand.ExecuteAsync(null);

        dashboard.ShowRiskBanner.Should().BeFalse();
    }
}
