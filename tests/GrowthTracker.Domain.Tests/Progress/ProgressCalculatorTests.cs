using GrowthTracker.Domain.Progress;

namespace GrowthTracker.Domain.Tests.Progress;

public class ProgressCalculatorTests
{
    [Fact]
    public void Plan_UsesCompletionNotTickCount()
    {
        // 3 sessions done at 100%, 50%, 50% out of 10 planned → 20%, not 30% (BR-08).
        ProgressCalculator.Plan([100m, 50m, 50m], plannedSessions: 10).Should().Be(20m);
    }

    [Fact]
    public void Plan_IsCappedAt100_AndHandlesZeroPlanned()
    {
        ProgressCalculator.Plan([100m, 100m, 100m], 2).Should().Be(100m);
        ProgressCalculator.Plan([100m], 0).Should().Be(0m);
    }

    [Fact]
    public void WeightedAverage_RespectsWeights_AndIgnoresNonPositive()
    {
        ProgressCalculator.WeightedAverage([(80m, 3m), (40m, 1m), (0m, 0m)]).Should().Be(70m);
        ProgressCalculator.WeightedAverage([]).Should().Be(0m);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(4, 1)]
    public void PriorityWeight_HighestPriorityWeighsMost(int priority, decimal weight)
    {
        ProgressCalculator.PriorityWeight(priority).Should().Be(weight);
    }

    [Fact]
    public void Expected_IsElapsedOverTotalDays_Clamped()
    {
        var start = new DateOnly(2027, 1, 1);
        var target = new DateOnly(2027, 1, 11);

        ProgressCalculator.Expected(start, target, new DateOnly(2027, 1, 6)).Should().Be(50m);
        ProgressCalculator.Expected(start, target, new DateOnly(2026, 12, 1)).Should().Be(0m);
        ProgressCalculator.Expected(start, target, new DateOnly(2027, 3, 1)).Should().Be(100m);
    }
}
