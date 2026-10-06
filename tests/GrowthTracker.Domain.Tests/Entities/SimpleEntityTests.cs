using GrowthTracker.Domain.Areas;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Milestones;
using GrowthTracker.Domain.PlanLogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;

namespace GrowthTracker.Domain.Tests.Entities;

public class SimpleEntityTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Area_Defaults_AreSixWithBrandColors()
    {
        Area.Defaults.Should().HaveCount(6);
        Area.Defaults[0].Should().Be(("Career", "#2563EB"));
    }

    [Fact]
    public void Area_RenameArchiveAndValidation()
    {
        var id = Guid.CreateVersion7();
        var area = new Area(id, " Career ", "#2563EB", 0);
        area.Id.Should().Be(id);
        area.Name.Should().Be("Career");

        area.Archive();
        area.IsArchived.Should().BeTrue();
        ((Action)(() => area.Rename(" "))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Entity_IdsAreVersion7_AndSoftDeletable()
    {
        var area = new Area("A", "#000000", 0);
        area.Id.Version.Should().Be(7);

        area.SoftDelete();
        area.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void UserProfile_StoresSettingsAndPin()
    {
        var profile = new UserProfile("Alex", "vi-VN", "SE Asia Standard Time");
        profile.WeekStartsOn.Should().Be(DayOfWeek.Monday);

        profile.SetPinHash("hash");
        profile.PinHash.Should().Be("hash");
    }

    [Fact]
    public void Vision_RequiresTitle()
    {
        new Vision(" Engineer ", null).Title.Should().Be("Engineer");
        ((Action)(() => _ = new Vision("", null))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Milestone_CompletesAt100_AndValidates()
    {
        var m = new Milestone(Guid.NewGuid(), "Master System Design", 0);
        m.Weight.Should().Be(1m);

        m.ApplyProgress(100m);
        m.Status.Should().Be(MilestoneStatus.Completed);

        ((Action)(() => _ = new Milestone(Guid.NewGuid(), "", 0))).Should().Throw<DomainException>();
        ((Action)(() => _ = new Milestone(Guid.NewGuid(), "M", 0, weight: 0))).Should().Throw<DomainException>();
    }

    [Fact]
    public void XpLedgerEntry_And_WeeklyReview_HoldValues()
    {
        var entry = new XpLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), 103.5m, "PlanLogged");
        entry.Amount.Should().Be(103.5m);

        var review = new WeeklyReview(new DateOnly(2026, 10, 5), 78, 12, 15, 80m, 950m, "{}");
        review.Complete("Good week", Now);
        review.CompletedAt.Should().Be(Now);
        review.Note.Should().Be("Good week");
    }

    [Fact]
    public void Recommendation_MarkApplied()
    {
        var r = new Recommendation(Guid.NewGuid(), RecommendationKind.IncreaseTarget, 20, 30, "85%");
        r.MarkApplied();
        r.State.Should().Be(RecommendationState.Applied);
    }

    [Fact]
    public void AchievementCriterion_SetDone_TracksTimestamp()
    {
        var c = new AchievementCriterion(Guid.NewGuid(), "Take the exam");
        c.SetDone(true, Now);
        c.DoneAt.Should().Be(Now);

        c.SetDone(false, Now);
        c.DoneAt.Should().BeNull();
    }

    [Fact]
    public void Plan_Archive_And_Validation()
    {
        var start = new DateOnly(2026, 10, 1);
        var plan = new Plan("Read", PlanType.Binary, 0, null, start, start.AddDays(30), Difficulty.Easy, [(Guid.NewGuid(), 1m)]);
        plan.Archive();
        plan.Status.Should().Be(PlanStatus.Archived);

        ((Action)(() => _ = new Plan("", PlanType.Binary, 0, null, start, start, Difficulty.Easy, [(Guid.NewGuid(), 1m)])))
            .Should().Throw<DomainException>();
        ((Action)(() => _ = new Plan("P", PlanType.Binary, 0, null, start, start.AddDays(-1), Difficulty.Easy, [(Guid.NewGuid(), 1m)])))
            .Should().Throw<DomainException>();
        ((Action)(() => _ = new Plan("P", PlanType.Binary, 0, null, start, start, Difficulty.Easy, [(Guid.NewGuid(), 0m)])))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Result_SuccessAndFailure()
    {
        Result<int>.Success(5).Should().Match<Result<int>>(r => r.IsSuccess && r.Value == 5);
        var failure = Result<int>.Failure("x", "boom");
        failure.IsSuccess.Should().BeFalse();
        failure.Error!.Code.Should().Be("x");
    }

    [Fact]
    public void WeekDays_Includes()
    {
        WeekDays.Weekdays.Includes(DayOfWeek.Friday).Should().BeTrue();
        WeekDays.Weekdays.Includes(DayOfWeek.Sunday).Should().BeFalse();
    }
}
