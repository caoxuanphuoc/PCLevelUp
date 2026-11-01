using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.PlanLogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;
using GrowthTracker.Domain.Scoring;

namespace GrowthTracker.Domain.Tests.Entities;

public class PlanTests
{
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly (Guid, decimal)[] OneGoal = [(Guid.NewGuid(), 1m)];

    [Fact]
    public void Plan_WithoutGoal_Throws_BR01()
    {
        var act = () => new Plan("Study", PlanType.Time, 45, "min", Start, Start.AddDays(90), Difficulty.Normal, []);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("plan.goal_required");
    }

    [Theory]
    [InlineData(PlanType.Time, 0, null, "plan.target_required")]
    [InlineData(PlanType.Quantity, -1, null, "plan.target_required")]
    [InlineData(PlanType.Frequency, 0, null, "plan.frequency_required")]
    [InlineData(PlanType.Frequency, 0, 8, "plan.frequency_required")]
    [InlineData(PlanType.Achievement, 0, null, "plan.criteria_required")]
    public void Plan_WithoutMeasurement_Throws_BR02(PlanType type, decimal target, int? perWeek, string code)
    {
        var act = () => new Plan("P", type, target, null, Start, Start.AddDays(10), Difficulty.Easy, OneGoal,
                                 frequencyPerWeek: perWeek);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void Plan_Achievement_StoresCriteria()
    {
        var plan = new Plan("IELTS", PlanType.Achievement, 0, null, Start, Start.AddDays(10), Difficulty.Hard, OneGoal,
                            criteria: ["Submit application", "Take the exam"]);

        plan.Criteria.Should().HaveCount(2).And.OnlyContain(c => c.PlanId == plan.Id);
        plan.GoalLinks.Should().ContainSingle().Which.PlanId.Should().Be(plan.Id);
    }

    [Fact]
    public void UpdateTarget_ForFrequency_UpdatesSessionsPerWeek()
    {
        var plan = new Plan("Gym", PlanType.Frequency, 5, null, Start, Start.AddDays(90), Difficulty.Hard, OneGoal,
                            frequencyPerWeek: 5);

        plan.UpdateTarget(3);

        plan.FrequencyPerWeek.Should().Be(3);
    }

    [Fact]
    public void HasEnded_AfterTheEndDate_OnlyForActivePlans()
    {
        var plan = new Plan("Read", PlanType.Binary, 1, null, Start, Start.AddDays(10), Difficulty.Easy, OneGoal);

        plan.HasEnded(Start.AddDays(10)).Should().BeFalse(); // the last day still runs
        plan.HasEnded(Start.AddDays(11)).Should().BeTrue();
        plan.Archive();
        plan.HasEnded(Start.AddDays(11)).Should().BeFalse(); // archived, not ended
    }

    [Fact]
    public void Edit_ChangesEverythingButTypeAndStart()
    {
        var plan = new Plan("Gym", PlanType.Frequency, 5, "buổi", Start, Start.AddDays(30), Difficulty.Hard, OneGoal, frequencyPerWeek: 5);

        plan.Edit(" Gym nhẹ ", 0, "buổi", WeekDays.Tuesday | WeekDays.Friday, 2, new TimeOnly(6, 0), Difficulty.Normal,
                  Start.AddDays(60), today: Start.AddDays(40));

        plan.Name.Should().Be("Gym nhẹ");
        plan.FrequencyPerWeek.Should().Be(2);
        plan.TargetValue.Should().Be(2); // a Frequency target is its sessions per week
        plan.DaysOfWeek.Should().Be(WeekDays.Tuesday | WeekDays.Friday);
        plan.PreferredTime.Should().Be(new TimeOnly(6, 0));
        plan.Difficulty.Should().Be(Difficulty.Normal);
        plan.DurationEnd.Should().Be(Start.AddDays(60));
        (plan.PlanType, plan.DurationStart).Should().Be((PlanType.Frequency, Start));
    }

    [Fact]
    public void Edit_NewEndDateInThePast_Throws_FR24()
    {
        var plan = new Plan("Read", PlanType.Binary, 1, null, Start, Start.AddDays(30), Difficulty.Easy, OneGoal, WeekDays.All);
        var today = Start.AddDays(40); // the plan has ended

        // Keeping the old (past) end date is fine: renaming an ended plan must work.
        plan.Edit("Read more", 1, null, WeekDays.All, null, null, Difficulty.Easy, Start.AddDays(30), today);
        var act = () => plan.Edit("Read", 1, null, WeekDays.All, null, null, Difficulty.Easy, Start.AddDays(35), today);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("plan.end_in_past");
    }

    [Theory]
    [InlineData(-1, "plan.duration")]
    [InlineData(10, "plan.not_active")]
    public void Edit_InvalidOrArchived_Throws(int endOffset, string code)
    {
        var plan = new Plan("Read", PlanType.Binary, 1, null, Start, Start.AddDays(30), Difficulty.Easy, OneGoal, WeekDays.All);
        if (code == "plan.not_active")
            plan.Archive();

        var act = () => plan.Edit("Read", 1, null, WeekDays.All, null, null, Difficulty.Easy, Start.AddDays(endOffset), Start);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }
}

public class OccurrenceAndLogTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);
    private static readonly DateTime Now = new(2026, 10, 15, 13, 0, 0, DateTimeKind.Utc);
    private static readonly ScoreBreakdown Breakdown = new(90, 80, 100, 90, 90, 103.5m, 2);

    [Fact]
    public void Record_ForFutureDate_Throws_FR24()
    {
        var occurrence = new PlanOccurrence(Guid.NewGuid(), Today.AddDays(1));

        var act = () => PlanLog.Record(occurrence, 30, 4, null, Breakdown, Today, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("planlog.future_date");
    }

    [Fact]
    public void Record_MarksOccurrenceDone_AndRaisesEvent()
    {
        var occurrence = new PlanOccurrence(Guid.NewGuid(), Today);

        var log = PlanLog.Record(occurrence, 27, 4, "Focused", Breakdown, Today, Now);

        occurrence.State.Should().Be(OccurrenceState.Done);
        log.Score.Should().Be(90);
        log.ScoreVersion.Should().Be(2);
        log.EditableUntil.Should().Be(Now.AddHours(24));
        log.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanLogged>();
    }

    [Fact]
    public void Record_NotDone_MarksSessionMissed_AndEditCanFlipIt()
    {
        var occurrence = new PlanOccurrence(Guid.NewGuid(), Today);

        var log = PlanLog.Record(occurrence, 0, 3, null, Breakdown, Today, Now, countsAsDone: false);
        occurrence.State.Should().Be(OccurrenceState.Missed);

        log.Edit(1, 4, null, Breakdown, Now.AddHours(1), occurrence, countsAsDone: true);
        occurrence.State.Should().Be(OccurrenceState.Done);

        var other = new PlanOccurrence(Guid.NewGuid(), Today);
        ((Action)(() => log.Edit(1, 4, null, Breakdown, Now.AddHours(1), other))).Should().Throw<DomainException>()
            .Which.Code.Should().Be("planlog.occurrence");
    }

    [Fact]
    public void Record_Twice_Throws()
    {
        var occurrence = new PlanOccurrence(Guid.NewGuid(), Today);
        PlanLog.Record(occurrence, 27, 4, null, Breakdown, Today, Now);

        var act = () => PlanLog.Record(occurrence, 27, 4, null, Breakdown, Today, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Edit_After24Hours_Throws_BR11()
    {
        var log = PlanLog.Record(new PlanOccurrence(Guid.NewGuid(), Today), 27, 4, null, Breakdown, Today, Now);

        var act = () => log.Edit(30, 5, null, Breakdown with { Score = 95 }, Now.AddHours(24).AddSeconds(1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("planlog.edit_window");
    }

    [Fact]
    public void Edit_Within24Hours_RecomputesScore()
    {
        var log = PlanLog.Record(new PlanOccurrence(Guid.NewGuid(), Today), 27, 4, null, Breakdown, Today, Now);

        log.Edit(30, 5, "fixed", Breakdown with { Score = 95 }, Now.AddHours(23));

        log.Score.Should().Be(95);
        log.Quality.Should().Be(5);
    }

    [Fact]
    public void Reschedule_IntoPast_Throws_FR24()
    {
        var occurrence = new PlanOccurrence(Guid.NewGuid(), Today);

        var act = () => occurrence.Reschedule(Today.AddDays(-1), Today, null, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("occurrence.past_date");
    }

    [Fact]
    public void Reschedule_KeepsOriginalDate_AndCounts()
    {
        var occurrence = new PlanOccurrence(Guid.NewGuid(), Today);

        occurrence.Reschedule(Today.AddDays(2), Today, "Busy", Now);

        occurrence.ScheduledDate.Should().Be(Today.AddDays(2));
        occurrence.OriginalDate.Should().Be(Today);
        occurrence.RescheduleCount.Should().Be(1);
        occurrence.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanRescheduled>();
    }

    [Fact]
    public void Skip_And_Missed_Transitions()
    {
        var skipped = new PlanOccurrence(Guid.NewGuid(), Today);
        skipped.Skip(SkipReason.Sick, Today);
        skipped.State.Should().Be(OccurrenceState.Skipped);

        var overdue = new PlanOccurrence(Guid.NewGuid(), Today.AddDays(-1));
        overdue.MarkMissedIfOverdue(Today);
        overdue.State.Should().Be(OccurrenceState.Missed);

        var future = new PlanOccurrence(Guid.NewGuid(), Today.AddDays(1));
        ((Action)(() => future.Skip(SkipReason.Busy, Today))).Should().Throw<DomainException>();
    }
}

public class GoalTests
{
    private static Goal NewGoal() =>
        new(Guid.NewGuid(), "Become Senior .NET Developer", new DateOnly(2026, 10, 1), new DateOnly(2028, 10, 1), 1);

    [Fact]
    public void Goal_InvalidDatesOrPriority_Throw()
    {
        var start = new DateOnly(2026, 10, 1);
        ((Action)(() => _ = new Goal(Guid.NewGuid(), "G", start, start, 1))).Should().Throw<DomainException>();
        ((Action)(() => _ = new Goal(Guid.NewGuid(), "G", start, start.AddDays(1), 5))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Goal_Lifecycle_DraftActiveAtRiskOnTrack()
    {
        var goal = NewGoal();
        goal.ApplyProgress(10, 30, null, isAtRisk: true);
        goal.Status.Should().Be(GoalStatus.Draft); // a draft goal is not evaluated

        goal.Activate();
        goal.ApplyProgress(38, 55, 65, isAtRisk: true);
        goal.Status.Should().Be(GoalStatus.AtRisk);

        goal.ApplyProgress(50, 55, 70, isAtRisk: false);
        goal.Status.Should().Be(GoalStatus.OnTrack);
        goal.Progress.Should().Be(50);
    }

    [Fact]
    public void Goal_CancelledCannotComplete()
    {
        var goal = NewGoal();
        goal.Cancel();

        ((Action)goal.Complete).Should().Throw<DomainException>();
    }

    [Fact]
    public void Recommendation_Dismiss_SnoozesFourWeeks()
    {
        var r = new Recommendation(Guid.NewGuid(), RecommendationKind.DecreaseTarget, 5, 3, "48%");
        r.Dismiss(new DateOnly(2026, 11, 1), 4);

        r.State.Should().Be(RecommendationState.Dismissed);
        r.SnoozedUntil.Should().Be(new DateOnly(2026, 11, 29));
    }
}
