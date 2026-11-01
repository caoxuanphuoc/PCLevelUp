using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Calendar;
using GrowthTracker.Application.Dashboard;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Jobs;
using GrowthTracker.Application.Settings;
using GrowthTracker.Application.Logging;
using GrowthTracker.Application.Plans;
using GrowthTracker.Application.Reviews;
using GrowthTracker.Domain.Gamification;
using GrowthTracker.Domain.Risk;
using GrowthTracker.Domain.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers domain services and use-case handlers. Handlers are registered explicitly here
    /// (no MediatR): one Command/Query + Handler pair per use case, in its feature folder.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(ScoreWeights.Default);
        services.AddSingleton(DifficultyTable.Default);
        services.AddSingleton<ScoreEngine>(sp => new ScoreEngine(
            sp.GetRequiredService<ScoreWeights>(), sp.GetRequiredService<DifficultyTable>()));
        services.AddSingleton(DomainThresholds.Default);
        services.AddSingleton<RiskDetector>(sp => new RiskDetector(sp.GetRequiredService<DomainThresholds>()));
        services.AddSingleton<Recommender>(sp => new Recommender(sp.GetRequiredService<DomainThresholds>()));

        services.AddSingleton(LevelTable.Default);
        services.AddSingleton<IUseCaseDispatcher, UseCaseDispatcher>();
        services.AddScoped<LogEffects>();
        services.AddScoped<LogResultBuilder>();

        // Areas
        services.AddQuery<GetAreasQuery, IReadOnlyList<AreaDto>, GetAreasHandler>();
        services.AddCommand<CreateAreaCommand, Guid, CreateAreaHandler>();
        services.AddCommand<UpdateAreaCommand, Guid, UpdateAreaHandler>();
        services.AddCommand<SetAreaArchivedCommand, Guid, SetAreaArchivedHandler>();

        // Goals & milestones
        services.AddQuery<GetGoalsQuery, GoalsPageDto, GetGoalsHandler>();
        services.AddQuery<GetGoalDetailQuery, GoalDetailDto?, GetGoalDetailHandler>();
        services.AddCommand<CreateGoalCommand, Guid, CreateGoalHandler>();
        services.AddCommand<UpdateGoalCommand, Guid, UpdateGoalHandler>();
        services.AddCommand<CancelGoalCommand, Guid, CancelGoalHandler>();
        services.AddCommand<AddMilestoneCommand, Guid, AddMilestoneHandler>();
        services.AddCommand<RemoveMilestoneCommand, Guid, RemoveMilestoneHandler>();

        // Plans
        services.AddCommand<CreatePlanCommand, CreatePlanResult, CreatePlanHandler>();
        services.AddCommand<ArchivePlanCommand, Guid, ArchivePlanHandler>();
        services.AddCommand<UpdatePlanCommand, UpdatePlanResult, UpdatePlanHandler>();

        // Daily logging
        services.AddQuery<GetTodayQuery, TodayDto, GetTodayHandler>();
        services.AddQuery<GetPlanResultFormQuery, PlanResultFormDto?, GetPlanResultFormHandler>();
        services.AddQuery<GetLogResultQuery, LogResultDto?, GetLogResultHandler>();
        services.AddCommand<LogPlanResultCommand, LogResultDto, LogPlanResultHandler>();
        services.AddCommand<EditPlanLogCommand, LogResultDto, EditPlanLogHandler>();
        services.AddCommand<SkipOccurrenceCommand, Guid, SkipOccurrenceHandler>();

        // Calendar
        services.AddQuery<GetCalendarQuery, CalendarDto, GetCalendarHandler>();
        services.AddQuery<GetSchedulablePlansQuery, IReadOnlyList<SchedulablePlanDto>, GetSchedulablePlansHandler>();
        services.AddCommand<RescheduleOccurrenceCommand, int, RescheduleOccurrenceHandler>();
        services.AddCommand<AddSessionCommand, Guid, AddSessionHandler>();

        // Dashboard
        services.AddQuery<GetDashboardSummaryQuery, DashboardDto, GetDashboardSummaryHandler>();
        services.AddQuery<GetScoreTrendQuery, ScoreTrendDto, GetScoreTrendHandler>();
        services.AddCommand<SnoozeRiskBannerCommand, DateOnly, SnoozeRiskBannerHandler>();

        // Settings and background jobs
        services.AddQuery<GetAppSettingsQuery, AppSettingsDto, GetAppSettingsHandler>();
        services.AddCommand<UpdateBackgroundSettingsCommand, AppSettingsDto, UpdateBackgroundSettingsHandler>();
        services.AddCommand<RunRolloverCommand, RolloverResult, RunRolloverHandler>();
        services.AddCommand<RunRiskScanCommand, int, RunRiskScanHandler>();
        services.AddQuery<GetDueRemindersQuery, IReadOnlyList<ReminderDto>, GetDueRemindersHandler>();
        services.AddCommand<RunWeeklyReviewJobCommand, WeeklyReviewJobResult?, RunWeeklyReviewJobHandler>();
        services.AddCommand<RunDailyBackupCommand, string?, RunDailyBackupHandler>();
        services.AddSingleton<JobScheduler>();

        // Weekly review
        services.AddScoped<ReviewCalculator>();
        services.AddQuery<GetWeeklyReviewQuery, WeeklyReviewDto, GetWeeklyReviewHandler>();
        services.AddCommand<GenerateWeeklyReviewCommand, Guid, GenerateWeeklyReviewHandler>();
        services.AddCommand<SaveWeeklyReviewNoteCommand, Guid, SaveWeeklyReviewNoteHandler>();
        services.AddCommand<CompleteWeeklyReviewCommand, Guid, CompleteWeeklyReviewHandler>();
        services.AddCommand<ApplyRecommendationCommand, Guid, ApplyRecommendationHandler>();
        services.AddCommand<DismissRecommendationCommand, Guid, DismissRecommendationHandler>();

        return services;
    }

    private static void AddCommand<TCommand, TResult, THandler>(this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult> =>
        services.AddScoped<ICommandHandler<TCommand, TResult>, THandler>();

    private static void AddQuery<TQuery, TResult, THandler>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where THandler : class, IQueryHandler<TQuery, TResult> =>
        services.AddScoped<IQueryHandler<TQuery, TResult>, THandler>();
}
