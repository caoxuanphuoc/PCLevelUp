namespace GrowthTracker.Domain.Gamification;

public sealed record Streak(int Current, int Longest);

/// <summary>
/// A streak day is a day with at least one Done log. Rest days neither break nor extend a streak.
/// Today still counts as "in progress": if nothing is done yet today, the current streak runs up to yesterday.
/// </summary>
public static class StreakCalculator
{
    public static Streak Calculate(IEnumerable<DateOnly> doneDays, IEnumerable<DateOnly> restDays, DateOnly today)
    {
        var done = doneDays.Where(d => d <= today).ToHashSet();
        var rest = restDays.ToHashSet();
        if (done.Count == 0)
            return new Streak(0, 0);

        int current = 0;
        var day = done.Contains(today) ? today : today.AddDays(-1);
        for (; ; day = day.AddDays(-1))
        {
            if (done.Contains(day))
                current++;
            else if (!rest.Contains(day))
                break;
        }

        int longest = 0, run = 0;
        var first = done.Min();
        for (var d = first; d <= today; d = d.AddDays(1))
        {
            if (done.Contains(d))
                longest = Math.Max(longest, ++run);
            else if (!rest.Contains(d))
                run = 0;
        }

        return new Streak(current, Math.Max(longest, current));
    }
}
