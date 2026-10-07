using System.Linq.Expressions;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Areas;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.PlanLogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;
using GrowthTracker.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrowthTracker.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Vision> Visions => Set<Vision>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanGoalLink> PlanGoalLinks => Set<PlanGoalLink>();
    public DbSet<AchievementCriterion> AchievementCriteria => Set<AchievementCriterion>();
    public DbSet<PlanOccurrence> PlanOccurrences => Set<PlanOccurrence>();
    public DbSet<PlanLog> PlanLogs => Set<PlanLog>();
    public DbSet<XpLedgerEntry> XpLedger => Set<XpLedgerEntry>();
    public DbSet<WeeklyReview> WeeklyReviews => Set<WeeklyReview>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no native decimal; storing as REAL keeps SUM/ORDER BY in SQL.
        configurationBuilder.Properties<decimal>().HaveConversion<double>();
        configurationBuilder.Properties<decimal?>().HaveConversion<double?>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Ignore(nameof(Entity.DomainEvents));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(SoftDeleteFilter(entityType.ClrType));
        }
    }

    private static LambdaExpression SoftDeleteFilter(Type type)
    {
        var parameter = Expression.Parameter(type, "e");
        var isDeleted = Expression.Property(parameter, nameof(Entity.IsDeleted));
        return Expression.Lambda(Expression.Not(isDeleted), parameter);
    }
}
