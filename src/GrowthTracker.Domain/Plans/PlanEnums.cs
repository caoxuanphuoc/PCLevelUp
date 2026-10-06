namespace GrowthTracker.Domain.Plans;

/// <summary>The five ways a Plan is measured (BRD §5.2).</summary>
public enum PlanType
{
    Time,
    Quantity,
    Frequency,
    Binary,
    Achievement,
}

public enum Difficulty
{
    Easy,
    Normal,
    Hard,
    Extreme,
}

public enum PlanStatus
{
    Active,
    Paused,
    Archived,
}

/// <summary>Days-of-week bitmask stored on Plan.DaysOfWeek.</summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    All = Weekdays | Saturday | Sunday,
}

public static class WeekDaysExtensions
{
    public static WeekDays ToFlag(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        _ => WeekDays.Sunday,
    };

    public static bool Includes(this WeekDays days, DayOfWeek day) => (days & day.ToFlag()) != 0;
}
