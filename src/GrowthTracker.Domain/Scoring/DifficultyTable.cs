using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Domain.Scoring;

/// <summary>
/// Difficulty points (D component) and XP multipliers. Only Hard ×1.15 comes from the original BRD;
/// the other values are the BRD §5.3 proposals and still need sign-off.
/// </summary>
public sealed class DifficultyTable(IReadOnlyDictionary<Difficulty, (decimal Points, decimal XpMultiplier)> values)
{
    public static readonly DifficultyTable Default = new(new Dictionary<Difficulty, (decimal, decimal)>
    {
        [Difficulty.Easy] = (60m, 1.00m),
        [Difficulty.Normal] = (75m, 1.05m),
        [Difficulty.Hard] = (90m, 1.15m),
        [Difficulty.Extreme] = (100m, 1.30m),
    });

    public decimal Points(Difficulty difficulty) => values[difficulty].Points;

    public decimal XpMultiplier(Difficulty difficulty) => values[difficulty].XpMultiplier;
}
