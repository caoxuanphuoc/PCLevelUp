using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;
using GrowthTracker.Domain.Risk;

namespace GrowthTracker.Domain.Tests.Risk;

public class RiskDetectorTests
{
    private readonly RiskDetector _detector = new();

    [Fact]
    public void Goal_Expected55Actual38_IsAtRisk()
    {
        var risk = _detector.EvaluateGoal(actualProgress: 38m, expectedProgress: 55m);

        risk.IsAtRisk.Should().BeTrue();
        risk.GapPoints.Should().Be(17m);
    }

    [Theory]
    [InlineData(45, 55)] // exactly 10 points behind is still On Track
    [InlineData(60, 55)]
    public void Goal_WithinTenPoints_IsOnTrack(decimal actual, decimal expected)
    {
        _detector.EvaluateGoal(actual, expected).IsAtRisk.Should().BeFalse();
    }

    [Fact]
    public void Plan_5Of14Days_FlagsTooHard()
    {
        var risk = _detector.EvaluatePlan(sessionsDone: 5, sessionsScheduled: 14, daysOfData: 14);

        risk.IsTooHard.Should().BeTrue();
        Math.Round(risk.ConsistencyPct).Should().Be(36m);
    }

    [Fact]
    public void Plan_WithTooLittleData_IsNotFlagged()
    {
        _detector.EvaluatePlan(1, 6, daysOfData: 6).IsTooHard.Should().BeFalse(); // BR-13
    }

    [Fact]
    public void Plan_WithNoScheduledSessions_IsNotFlagged()
    {
        _detector.EvaluatePlan(0, 0, 14).Should().Be(new PlanRisk(false, 0m));
    }
}

public class RecommenderTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);
    private readonly Recommender _recommender = new();

    [Fact]
    public void Gym_48PercentIn4Weeks_SuggestsDecreaseTo3()
    {
        var suggestion = _recommender.Suggest(new RecommendationInput(
            PlanType.Frequency, CurrentTarget: 5, CompletionRatePct: 48, ConsistencyPct: 48, DaysOfData: 28, Today));

        suggestion.Should().NotBeNull();
        suggestion!.Kind.Should().Be(RecommendationKind.DecreaseTarget);
        suggestion.SuggestedValue.Should().Be(3m);
    }

    [Fact]
    public void EnglishSpeaking_85Percent_SuggestsIncreaseTo30()
    {
        var suggestion = _recommender.Suggest(new RecommendationInput(
            PlanType.Time, CurrentTarget: 20, CompletionRatePct: 90, ConsistencyPct: 85, DaysOfData: 28, Today));

        suggestion.Should().NotBeNull();
        suggestion!.Kind.Should().Be(RecommendationKind.IncreaseTarget);
        suggestion.SuggestedValue.Should().Be(30m);
    }

    [Fact]
    public void LessThanFourWeeksOfData_NoSuggestion()
    {
        _recommender.Suggest(new RecommendationInput(PlanType.Frequency, 5, 20, 20, DaysOfData: 27, Today))
            .Should().BeNull();
    }

    [Fact]
    public void DismissedWithinFourWeeks_NoSuggestion()
    {
        var input = new RecommendationInput(PlanType.Frequency, 5, 20, 20, 60, Today, LastDismissedOn: Today.AddDays(-27));

        _recommender.Suggest(input).Should().BeNull();
        _recommender.Suggest(input with { LastDismissedOn = Today.AddDays(-28) }).Should().NotBeNull();
    }

    [Fact]
    public void MidRange_NoSuggestion()
    {
        _recommender.Suggest(new RecommendationInput(PlanType.Time, 30, 70, 70, 28, Today)).Should().BeNull();
    }

    [Theory]
    [InlineData(PlanType.Binary)]
    [InlineData(PlanType.Achievement)]
    public void BinaryAndAchievement_NeverGetSuggestions(PlanType type)
    {
        _recommender.Suggest(new RecommendationInput(type, 1, 10, 10, 60, Today)).Should().BeNull();
    }

    [Fact]
    public void Decrease_AtMinimumTarget_NoSuggestion()
    {
        _recommender.Suggest(new RecommendationInput(PlanType.Frequency, 1, 10, 10, 28, Today)).Should().BeNull();
    }

    [Fact]
    public void Decrease_RoundsTimeUpToFiveMinutes_AndAlwaysLowers()
    {
        _recommender.Suggest(new RecommendationInput(PlanType.Time, 45, 50, 50, 28, Today))!
            .SuggestedValue.Should().Be(25m); // 22.5 → 25
        _recommender.Suggest(new RecommendationInput(PlanType.Quantity, 10, 59, 59, 28, Today))!
            .SuggestedValue.Should().Be(6m);
    }

    [Theory]
    [InlineData(PlanType.Quantity, 10, 13)]
    [InlineData(PlanType.Frequency, 3, 4)]
    public void Increase_PerPlanType(PlanType type, decimal current, decimal expected)
    {
        _recommender.Suggest(new RecommendationInput(type, current, 95, 95, 28, Today))!
            .SuggestedValue.Should().Be(expected);
    }

    [Fact]
    public void Increase_FrequencyAlreadyDaily_NoSuggestion()
    {
        _recommender.Suggest(new RecommendationInput(PlanType.Frequency, 7, 95, 95, 28, Today)).Should().BeNull();
    }
}
