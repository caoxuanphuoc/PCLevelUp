using GrowthTracker.Domain.Gamification;

namespace GrowthTracker.Domain.Tests.Gamification;

public class LevelTableTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(149, 1)]
    [InlineData(150, 2)]
    [InlineData(2100, 5)] // BRD: English Level 5 with 2,100 XP
    [InlineData(4250, 8)] // BRD: Career Level 8 with 4,250 XP
    public void Default_MatchesBrdExamples(decimal xp, int level)
    {
        LevelTable.Default.For(xp).Level.Should().Be(level);
    }

    [Fact]
    public void For_ReportsXpToNextLevel()
    {
        var info = LevelTable.Default.For(4250m);

        info.LevelStartXp.Should().Be(4200m);
        info.NextLevelXp.Should().Be(5400m);
        info.XpToNextLevel.Should().Be(1150m);
    }

    [Fact]
    public void For_AtMaxLevel_HasNoNextLevel()
    {
        var table = new LevelTable([0m, 100m]);

        table.For(500m).Should().Be(new LevelInfo(2, 500m, 100m, null));
    }

    [Fact]
    public void Constructor_RejectsInvalidThresholds()
    {
        ((Action)(() => _ = new LevelTable([10m]))).Should().Throw<ArgumentException>();
        ((Action)(() => _ = new LevelTable([0m, 100m, 100m]))).Should().Throw<ArgumentException>();
    }
}

public class XpCalculatorTests
{
    [Fact]
    public void Split_ByWeight_PreservesTotal()
    {
        var shares = XpCalculator.Split(100m, [("a", 1m), ("b", 1m), ("c", 1m)]);

        shares.Select(s => s.Amount).Should().Equal(33.33m, 33.33m, 33.34m);
        shares.Sum(s => s.Amount).Should().Be(100m);
    }

    [Fact]
    public void Split_SingleGoal_GetsEverything()
    {
        XpCalculator.Split(103.5m, [("career", 2m)]).Should().ContainSingle().Which.Amount.Should().Be(103.5m);
    }

    [Fact]
    public void Split_NoShares_ReturnsEmpty()
    {
        XpCalculator.Split<string>(10m, []).Should().BeEmpty();
    }
}

public class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static DateOnly D(int offset) => Today.AddDays(offset);

    [Fact]
    public void ConsecutiveDaysEndingToday_CountAsCurrent()
    {
        var streak = StreakCalculator.Calculate([D(-2), D(-1), D(0)], [], Today);

        streak.Should().Be(new Streak(3, 3));
    }

    [Fact]
    public void NothingDoneYetToday_DoesNotBreakStreak()
    {
        StreakCalculator.Calculate([D(-3), D(-2), D(-1)], [], Today).Current.Should().Be(3);
    }

    [Fact]
    public void MissedDay_BreaksStreak_ButLongestIsKept()
    {
        var streak = StreakCalculator.Calculate([D(-10), D(-9), D(-8), D(-7), D(-1), D(0)], [], Today);

        streak.Should().Be(new Streak(2, 4));
    }

    [Fact]
    public void RestDay_DoesNotBreakStreak()
    {
        var streak = StreakCalculator.Calculate([D(-3), D(-2), D(0)], [D(-1)], Today);

        streak.Should().Be(new Streak(3, 3));
    }

    [Fact]
    public void NoDoneDays_IsZero()
    {
        StreakCalculator.Calculate([], [], Today).Should().Be(new Streak(0, 0));
    }
}
