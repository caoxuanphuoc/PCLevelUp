using GrowthTracker.Domain.Areas;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.PlanLogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;
using GrowthTracker.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrowthTracker.Application.Abstractions;

/// <summary>Unit of work over the local SQLite database. Only use cases touch it, never ViewModels.</summary>
public interface IAppDbContext
{
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<Area> Areas { get; }
    DbSet<AppSetting> AppSettings { get; }
    DbSet<Vision> Visions { get; }
    DbSet<Goal> Goals { get; }
    DbSet<Milestone> Milestones { get; }
    DbSet<Plan> Plans { get; }
    DbSet<PlanGoalLink> PlanGoalLinks { get; }
    DbSet<AchievementCriterion> AchievementCriteria { get; }
    DbSet<PlanOccurrence> PlanOccurrences { get; }
    DbSet<PlanLog> PlanLogs { get; }
    DbSet<XpLedgerEntry> XpLedger { get; }
    DbSet<WeeklyReview> WeeklyReviews { get; }
    DbSet<Recommendation> Recommendations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>For use cases that must save in several steps but commit once (e.g. a log and its XP/progress effects).</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
