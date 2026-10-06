using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scheduling;

namespace GrowthTracker.Domain.Tests.Scheduling;

public class ScheduleGeneratorTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    [Fact]
    public void ExplicitDays_GenerateOnlyThoseDays_ForEightWeeks()
    {
        var plan = new ScheduleInput(PlanType.Time, Monday, Monday.AddDays(365),
                                     WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday, null);

        var dates = ScheduleGenerator.Generate(plan, Monday);

        dates.Should().HaveCount(24);
        dates.Should().OnlyContain(d => d.DayOfWeek == DayOfWeek.Monday
                                     || d.DayOfWeek == DayOfWeek.Wednesday
                                     || d.DayOfWeek == DayOfWeek.Friday);
        dates.Max().Should().BeBefore(Monday.AddDays(56));
    }

    [Fact]
    public void NeverSchedulesInThePast()
    {
        var plan = new ScheduleInput(PlanType.Binary, Monday.AddDays(-30), Monday.AddDays(30), WeekDays.None, null);
        var today = Monday.AddDays(3);

        ScheduleGenerator.Generate(plan, today).Should().OnlyContain(d => d >= today);
    }

    [Fact]
    public void RespectsDurationEnd_AndFutureStart()
    {
        var plan = new ScheduleInput(PlanType.Binary, Monday.AddDays(7), Monday.AddDays(9), WeekDays.None, null);

        ScheduleGenerator.Generate(plan, Monday).Should().Equal(Monday.AddDays(7), Monday.AddDays(8), Monday.AddDays(9));
    }

    [Fact]
    public void SkipsAlreadyScheduledDates()
    {
        var plan = new ScheduleInput(PlanType.Binary, Monday, Monday.AddDays(2), WeekDays.None, null);

        ScheduleGenerator.Generate(plan, Monday, new HashSet<DateOnly> { Monday.AddDays(1) })
            .Should().Equal(Monday, Monday.AddDays(2));
    }

    [Theory]
    [InlineData(3, WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday)]
    [InlineData(5, WeekDays.Weekdays)]
    [InlineData(7, WeekDays.All)]
    public void Frequency_WithoutDays_IsSpreadAcrossWeek(int perWeek, WeekDays expected)
    {
        var plan = new ScheduleInput(PlanType.Frequency, Monday, Monday.AddDays(6), WeekDays.None, perWeek);

        ScheduleGenerator.EffectiveDays(plan).Should().Be(expected);
        ScheduleGenerator.Generate(plan, Monday).Should().HaveCount(perWeek);
    }

    [Fact]
    public void TimePlan_WithoutDaysOrFrequency_GeneratesNothing()
    {
        var plan = new ScheduleInput(PlanType.Time, Monday, Monday.AddDays(6), WeekDays.None, null);

        ScheduleGenerator.Generate(plan, Monday).Should().BeEmpty();
    }

    [Fact]
    public void CountSessions_CoversWholeDuration()
    {
        // 3 sessions/week for 12 weeks
        var plan = new ScheduleInput(PlanType.Time, Monday, Monday.AddDays(83),
                                     WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday, null);

        ScheduleGenerator.CountSessions(plan).Should().Be(36);
    }
}
