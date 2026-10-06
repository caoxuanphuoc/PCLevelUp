namespace GrowthTracker.Domain.Progress;

/// <summary>
/// Bottom-up progress, Plan → Milestone → Goal → Area (BRD §5.5). All values are percentages 0..100.
/// Progress uses Completion rather than ticked counts, so it reflects how much was really done (BR-08).
/// </summary>
public static class ProgressCalculator
{
    /// <param name="completions">Completion (0..100) of each done session so far.</param>
    /// <param name="plannedSessions">Total sessions planned over the Plan's whole Duration.</param>
    public static decimal Plan(IEnumerable<decimal> completions, int plannedSessions)
    {
        if (plannedSessions <= 0)
            return 0m;
        decimal sum = completions.Sum(c => Math.Clamp(c, 0m, 100m));
        return Math.Min(100m, sum / plannedSessions);
    }

    /// <summary>Weighted average, used for Milestone (from Plans), Goal (from Milestones) and Area (from Goals).</summary>
    public static decimal WeightedAverage(IEnumerable<(decimal Progress, decimal Weight)> items)
    {
        decimal totalWeight = 0m, sum = 0m;
        foreach (var (progress, weight) in items)
        {
            if (weight <= 0)
                continue;
            totalWeight += weight;
            sum += progress * weight;
        }
        return totalWeight == 0 ? 0m : sum / totalWeight;
    }

    /// <summary>Area progress weighs active Goals by Priority: 1 (highest) → 4, 4 (lowest) → 1.</summary>
    public static decimal PriorityWeight(int priority) => 5 - Math.Clamp(priority, 1, 4);

    /// <summary>Expected progress = days elapsed ÷ total days from Start Date to Target Date.</summary>
    public static decimal Expected(DateOnly start, DateOnly target, DateOnly today)
    {
        int total = target.DayNumber - start.DayNumber;
        if (total <= 0)
            return today >= target ? 100m : 0m;
        int elapsed = Math.Clamp(today.DayNumber - start.DayNumber, 0, total);
        return elapsed * 100m / total;
    }
}
