using GrowthTracker.Domain.Progress;

namespace GrowthTracker.Domain.Tests.Progress;

public class TrendClassifierTests
{
    [Theory]
    [InlineData(78, 74, Trend.Improving)]
    [InlineData(77, 74, Trend.Improving)] // exactly the threshold
    [InlineData(76, 74, Trend.Stable)]
    [InlineData(70, 74, Trend.Declining)]
    public void Compare_UsesThreePointThreshold(decimal current, decimal previous, Trend expected)
    {
        TrendClassifier.Compare(current, previous, 3).Should().Be(expected);
    }

    [Fact]
    public void Classify_ComparesLaterHalfWithEarlierHalf_IgnoringEmptyWeeks()
    {
        TrendClassifier.Classify([60, null, 64, 70, 75, 80], 3).Should().Be(Trend.Improving);
        TrendClassifier.Classify([80, 79, 81, 80], 3).Should().Be(Trend.Stable);
        TrendClassifier.Classify([85, 80, 70, 65], 3).Should().Be(Trend.Declining);
    }

    [Fact]
    public void Classify_NeedsTwoWeeksOfData()
    {
        TrendClassifier.Classify([null, 70, null], 3).Should().BeNull();
        TrendClassifier.Classify([], 3).Should().BeNull();
    }
}
