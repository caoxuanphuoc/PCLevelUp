using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Domain.Scheduling;

public sealed record ScheduleInput(
    PlanType PlanType,
    DateOnly DurationStart,
    DateOnly DurationEnd,
    WeekDays DaysOfWeek,
    int? FrequencyPerWeek);

/// <summary>
/// Turns a Plan's Frequency / DaysOfWeek / Duration into session dates (FR-20).
/// Generates a rolling horizon (8 weeks by default) and never schedules in the past (FR-24).
/// </summary>
public static class ScheduleGenerator
{
    public const int DefaultHorizonWeeks = 8;

    /// <param name="alreadyScheduled">Dates that already have an occurrence; skipped so weekly top-ups stay idempotent.</param>
    public static IReadOnlyList<DateOnly> Generate(ScheduleInput plan, DateOnly today,
                                                   IReadOnlySet<DateOnly>? alreadyScheduled = null,
                                                   int horizonWeeks = DefaultHorizonWeeks)
    {
        var days = EffectiveDays(plan);
        if (days == WeekDays.None)
            return [];

        var from = plan.DurationStart > today ? plan.DurationStart : today;
        var horizonEnd = today.AddDays(7 * horizonWeeks - 1);
        var to = plan.DurationEnd < horizonEnd ? plan.DurationEnd : horizonEnd;

        var result = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (days.Includes(d.DayOfWeek) && alreadyScheduled?.Contains(d) != true)
                result.Add(d);
        }
        return result;
    }

    /// <summary>Number of sessions over the whole Duration (denominator for Plan progress).</summary>
    public static int CountSessions(ScheduleInput plan)
    {
        var days = EffectiveDays(plan);
        int count = 0;
        for (var d = plan.DurationStart; d <= plan.DurationEnd; d = d.AddDays(1))
            if (days.Includes(d.DayOfWeek))
                count++;
        return count;
    }

    /// <summary>
    /// Explicit DaysOfWeek wins. Otherwise a Frequency plan is spread across the week,
    /// and a Binary or Achievement plan without days is daily.
    /// </summary>
    public static WeekDays EffectiveDays(ScheduleInput plan)
    {
        if (plan.DaysOfWeek != WeekDays.None)
            return plan.DaysOfWeek;
        if (plan.FrequencyPerWeek is int n)
            return SpreadAcrossWeek(n);
        return plan.PlanType is PlanType.Binary or PlanType.Achievement ? WeekDays.All : WeekDays.None;
    }

    public static WeekDays SpreadAcrossWeek(int sessionsPerWeek) => Math.Clamp(sessionsPerWeek, 0, 7) switch
    {
        0 => WeekDays.None,
        1 => WeekDays.Monday,
        2 => WeekDays.Monday | WeekDays.Thursday,
        3 => WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday,
        4 => WeekDays.Monday | WeekDays.Tuesday | WeekDays.Thursday | WeekDays.Friday,
        5 => WeekDays.Weekdays,
        6 => WeekDays.Weekdays | WeekDays.Saturday,
        _ => WeekDays.All,
    };
}
