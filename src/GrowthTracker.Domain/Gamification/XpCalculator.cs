namespace GrowthTracker.Domain.Gamification;

public static class XpCalculator
{
    /// <summary>
    /// Splits one log's XP across the Goals a Plan contributes to, proportionally to link weights.
    /// Amounts are rounded to 2 decimals; any rounding remainder goes to the last share so the total is exact.
    /// </summary>
    public static IReadOnlyList<(TKey Key, decimal Amount)> Split<TKey>(decimal xp, IReadOnlyList<(TKey Key, decimal Weight)> shares)
    {
        if (shares.Count == 0)
            return [];

        decimal totalWeight = shares.Sum(s => s.Weight);
        if (totalWeight <= 0)
            throw new ArgumentException("Share weights must sum to a positive value.", nameof(shares));

        var result = new List<(TKey, decimal)>(shares.Count);
        decimal allocated = 0m;
        for (int i = 0; i < shares.Count; i++)
        {
            decimal amount = i == shares.Count - 1
                ? xp - allocated
                : Math.Round(xp * shares[i].Weight / totalWeight, 2, MidpointRounding.AwayFromZero);
            allocated += amount;
            result.Add((shares[i].Key, amount));
        }
        return result;
    }
}
