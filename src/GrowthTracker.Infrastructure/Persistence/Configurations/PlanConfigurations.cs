using GrowthTracker.Domain.Areas;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.PlanLogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrowthTracker.Infrastructure.Persistence.Configurations;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> b)
    {
        b.ToTable("Plan");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.TargetUnit).HasMaxLength(30);
        b.HasOne<Milestone>().WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.GoalLinks).WithOne().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.GoalLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Criteria).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PlanGoalLinkConfiguration : IEntityTypeConfiguration<PlanGoalLink>
{
    public void Configure(EntityTypeBuilder<PlanGoalLink> b)
    {
        b.ToTable("PlanGoalLink");
        b.HasKey(x => new { x.PlanId, x.GoalId });
        b.HasOne<Goal>().WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AchievementCriterionConfiguration : IEntityTypeConfiguration<AchievementCriterion>
{
    public void Configure(EntityTypeBuilder<AchievementCriterion> b)
    {
        b.ToTable("AchievementCriterion");
        b.Property(x => x.Title).HasMaxLength(200);
    }
}

internal sealed class PlanOccurrenceConfiguration : IEntityTypeConfiguration<PlanOccurrence>
{
    public void Configure(EntityTypeBuilder<PlanOccurrence> b)
    {
        b.ToTable("PlanOccurrence");
        b.Property(x => x.RescheduleReason).HasMaxLength(500);
        b.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ScheduledDate, x.State });  // Today, calendar month/week
        b.HasIndex(x => new { x.PlanId, x.ScheduledDate }); // Consistency 7 days, risk 14 days
    }
}

internal sealed class PlanLogConfiguration : IEntityTypeConfiguration<PlanLog>
{
    public void Configure(EntityTypeBuilder<PlanLog> b)
    {
        b.ToTable("PlanLog");
        b.HasOne<PlanOccurrence>().WithOne().HasForeignKey<PlanLog>(x => x.OccurrenceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.LoggedAt); // score trend, weekly review
    }
}

internal sealed class XpLedgerEntryConfiguration : IEntityTypeConfiguration<XpLedgerEntry>
{
    public void Configure(EntityTypeBuilder<XpLedgerEntry> b)
    {
        b.ToTable("XpLedgerEntry");
        b.Property(x => x.Reason).HasMaxLength(100);
        b.HasOne<PlanLog>().WithMany().HasForeignKey(x => x.PlanLogId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AreaId); // total XP and level per Area
    }
}

internal sealed class WeeklyReviewConfiguration : IEntityTypeConfiguration<WeeklyReview>
{
    public void Configure(EntityTypeBuilder<WeeklyReview> b)
    {
        b.ToTable("WeeklyReview");
        b.HasIndex(x => x.WeekStart).IsUnique();
    }
}

internal sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> b)
    {
        b.ToTable("Recommendation");
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<WeeklyReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.SetNull);
    }
}
