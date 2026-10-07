namespace GrowthTracker.Domain.Progress;

/// <summary>Score direction over time (FR-41, FR-42): shown as ↑ → ↓ and "Improving / Stable / Declining".</summary>
public enum Trend
{
    Declining,
    Stable,
    Improving,
}

public static class TrendClassifier
{
    /// <summary>Current vs previous period: a change of at least <paramref name="thresholdPoints"/> counts as a move.</summary>
    public static Trend Compare(decimal current, decimal previous, decimal thresholdPoints)
    {
        decimal delta = current - previous;
        return delta >= thresholdPoints ? Trend.Improving
             : delta <= -thresholdPoints ? Trend.Declining
             : Trend.Stable;
    }

    /// <summary>
    /// Multi-week trend: the average of the later half of the weeks that have a Score against the earlier half.
    /// Needs at least two weeks with data; null otherwise.
    /// </summary>
    public static Trend? Classify(IEnumerable<decimal?> weeklyScores, decimal thresholdPoints)
    {
        var scores = weeklyScores.Where(s => s is not null).Select(s => s!.Value).ToList();
        if (scores.Count < 2)
            return null;
        int half = scores.Count / 2;
        decimal earlier = scores.Take(half).Average();
        decimal later = scores.Skip(scores.Count - half).Average();
        return Compare(later, earlier, thresholdPoints);
    }
}
