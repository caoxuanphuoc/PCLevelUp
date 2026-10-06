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
        return services;
    }
}
