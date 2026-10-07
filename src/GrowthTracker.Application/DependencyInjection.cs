using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Goals;
using GrowthTracker.Application.Plans;
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

        services.AddSingleton<IUseCaseDispatcher, UseCaseDispatcher>();

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
