using GrowthTracker.Domain.Areas;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrowthTracker.Infrastructure.Persistence.Configurations;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("UserProfile");
        b.Property(x => x.DisplayName).HasMaxLength(100);
        b.Property(x => x.Locale).HasMaxLength(20);
        b.Property(x => x.TimeZone).HasMaxLength(100);
    }
}

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> b)
    {
        b.ToTable("Area");
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.ColorHex).HasMaxLength(9);
    }
}

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.ToTable("AppSetting");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100);
    }
}

internal sealed class VisionConfiguration : IEntityTypeConfiguration<Vision>
{
    public void Configure(EntityTypeBuilder<Vision> b)
    {
        b.ToTable("Vision");
        b.Property(x => x.Title).HasMaxLength(200);
    }
}

internal sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> b)
    {
        b.ToTable("Goal");
        b.Property(x => x.Name).HasMaxLength(200);
        b.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vision>().WithMany().HasForeignKey(x => x.VisionId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.AreaId, x.Status });
    }
}

internal sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> b)
    {
        b.ToTable("Milestone");
        b.Property(x => x.Name).HasMaxLength(200);
        b.HasOne<Goal>().WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
    }
}
