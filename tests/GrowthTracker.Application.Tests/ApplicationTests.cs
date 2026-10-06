using GrowthTracker.Application;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Risk;
using GrowthTracker.Domain.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersDomainServices_WithV2Default()
    {
        using var provider = new ServiceCollection().AddApplication().BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<ScoreEngine>().Weights.Should().Be(ScoreWeights.V2);
        provider.GetRequiredService<RiskDetector>().Should().NotBeNull();
        provider.GetRequiredService<Recommender>().Should().NotBeNull();
    }
}

public class ClockTests
{
    private sealed class FixedClock(DateTime utcNow, TimeZoneInfo timeZone) : IClock
    {
        public DateTime UtcNow => utcNow;
        public TimeZoneInfo TimeZone => timeZone;
    }

    private static readonly TimeZoneInfo Vietnam =
        TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");

    [Fact]
    public void Today_IsTheUsersLocalDate_NotTheUtcDate()
    {
        IClock clock = new FixedClock(new DateTime(2026, 10, 6, 18, 30, 0, DateTimeKind.Utc), Vietnam);

        clock.Today.Should().Be(new DateOnly(2026, 10, 7));
    }

    [Fact]
    public void Today_BeforeLocalMidnight_StaysOnSameDay()
    {
        IClock clock = new FixedClock(new DateTime(2026, 10, 6, 16, 59, 0, DateTimeKind.Utc), Vietnam);

        clock.Today.Should().Be(new DateOnly(2026, 10, 6));
    }
}
