using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scoring;

namespace GrowthTracker.Domain.Tests.Scoring;

public class ScoreEngineTests
{
    // BRD §5.3 example: C = 90, Q = 80 (4 stars), S = 100, D = 90 (Hard).
    private static ScoreInput BrdExample => new(
        PlanType.Time, Difficulty.Hard, Quality: 4,
        SessionsDoneLast7Days: 5, SessionsTargetLast7Days: 5,
        Actual: 27, Target: 30);

    [Fact]
    public void Score_BrdExample_V1_Returns90()
    {
        var result = new ScoreEngine(ScoreWeights.V1, DifficultyTable.Default).Calculate(BrdExample);

        result.Completion.Should().Be(90m);
        result.Quality.Should().Be(80m);
        result.Consistency.Should().Be(100m);
        result.DifficultyPoints.Should().Be(90m);
        result.Score.Should().Be(90); // 36 + 20 + 20 + 13.5 = 89.5, rounded away from zero
        result.ScoreVersion.Should().Be(1);
    }

    [Fact]
    public void Xp_Hard_Score90_Returns103_5()
    {
        var result = new ScoreEngine(ScoreWeights.V1, DifficultyTable.Default).Calculate(BrdExample);

        result.Xp.Should().Be(103.5m);
    }

    [Fact]
    public void Score_BrdExample_V2_Returns90()
    {
        var result = new ScoreEngine().Calculate(BrdExample);

        result.Score.Should().Be(90); // 40.5 + 24 + 25 = 89.5
        result.Xp.Should().Be(103.5m);
        result.ScoreVersion.Should().Be(2);
    }

    [Fact]
    public void Default_Weights_Are_V2()
    {
        new ScoreEngine().Weights.Should().Be(ScoreWeights.V2);
    }

    [Fact]
    public void Score_EasyPerfect_V2_Returns100()
    {
        var input = new ScoreInput(PlanType.Binary, Difficulty.Easy, Quality: 5, 7, 7, Done: true);

        new ScoreEngine().Calculate(input).Score.Should().Be(100);
    }

    [Fact]
    public void Score_EasyPerfect_V1_IsCappedBelow100_DifficultyCountedTwice()
    {
        var input = new ScoreInput(PlanType.Binary, Difficulty.Easy, Quality: 5, 7, 7, Done: true);

        new ScoreEngine(ScoreWeights.V1, DifficultyTable.Default).Calculate(input).Score.Should().Be(94);
    }

    [Theory]
    [InlineData(25, 30, 83.333)]
    [InlineData(30, 30, 100)]
    [InlineData(45, 30, 100)] // BR-09: overshoot does not add points
    [InlineData(0, 30, 0)]
    [InlineData(-5, 30, 0)]
    public void Completion_TimeAndQuantity_IsActualOverTargetCapped(decimal actual, decimal target, decimal expected)
    {
        var c = ScoreEngine.Completion(new ScoreInput(PlanType.Quantity, Difficulty.Normal, 3, 1, 1, actual, target));

        c.Should().BeApproximately(expected, 0.001m);
    }

    [Theory]
    [InlineData(PlanType.Binary, true, 100)]
    [InlineData(PlanType.Binary, false, 0)]
    [InlineData(PlanType.Frequency, true, 100)]
    [InlineData(PlanType.Frequency, false, 0)]
    public void Completion_BinaryAndFrequency_IsAllOrNothing(PlanType type, bool done, decimal expected)
    {
        ScoreEngine.Completion(new ScoreInput(type, Difficulty.Normal, 3, 1, 1, Done: done)).Should().Be(expected);
    }

    [Fact]
    public void Completion_Achievement_IsCriteriaRatio()
    {
        var input = new ScoreInput(PlanType.Achievement, Difficulty.Normal, 3, 1, 1, CriteriaDone: 1, CriteriaTotal: 4);

        ScoreEngine.Completion(input).Should().Be(25m);
    }

    [Fact]
    public void Completion_GuardsAgainstZeroTargets()
    {
        ScoreEngine.Completion(new ScoreInput(PlanType.Time, Difficulty.Normal, 3, 1, 1, 10, 0)).Should().Be(0);
        ScoreEngine.Completion(new ScoreInput(PlanType.Achievement, Difficulty.Normal, 3, 1, 1)).Should().Be(0);
    }

    [Theory]
    [InlineData(4, 5, 80)]
    [InlineData(7, 5, 100)]
    [InlineData(0, 5, 0)]
    [InlineData(1, 0, 100)]
    public void Consistency_IsDoneOverTargetCappedAt100(int done, int target, decimal expected)
    {
        ScoreEngine.Consistency(done, target).Should().Be(expected);
    }

    [Fact]
    public void Score_NeverExceeds100_EvenWithExtremeDifficulty()
    {
        var input = new ScoreInput(PlanType.Binary, Difficulty.Extreme, 5, 7, 7, Done: true);

        var result = new ScoreEngine(ScoreWeights.V1, DifficultyTable.Default).Calculate(input);

        result.Score.Should().Be(100);
        result.Xp.Should().Be(130m); // BR-06: XP may exceed 100
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Quality_OutsideOneToFive_Throws(int quality)
    {
        var act = () => new ScoreEngine().Calculate(new ScoreInput(PlanType.Binary, Difficulty.Easy, quality, 1, 1, Done: true));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ScoreWeights_FromVersion_RoundTrips()
    {
        ScoreWeights.FromVersion(1).Should().Be(ScoreWeights.V1);
        ScoreWeights.FromVersion(2).Should().Be(ScoreWeights.V2);
        var act = () => ScoreWeights.FromVersion(99);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
