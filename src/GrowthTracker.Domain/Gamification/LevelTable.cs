namespace GrowthTracker.Domain.Gamification;

public sealed record LevelInfo(int Level, decimal TotalXp, decimal LevelStartXp, decimal? NextLevelXp)
{
    public decimal? XpToNextLevel => NextLevelXp - TotalXp;
}

/// <summary>
/// Cumulative XP thresholds per level (index 0 = level 1 = 0 XP). The level formula is still open (BRD §9.3);
/// <see cref="Default"/> uses 75·L·(L−1), which matches the BRD examples (Level 8 at 4,250 XP, Level 5 at 2,100 XP).
/// </summary>
public sealed class LevelTable
{
    private readonly decimal[] _thresholds;

    public LevelTable(IEnumerable<decimal> thresholds)
    {
        _thresholds = thresholds.ToArray();
        if (_thresholds.Length == 0 || _thresholds[0] != 0m)
            throw new ArgumentException("The first threshold (level 1) must be 0 XP.", nameof(thresholds));
        for (int i = 1; i < _thresholds.Length; i++)
            if (_thresholds[i] <= _thresholds[i - 1])
                throw new ArgumentException("Thresholds must be strictly increasing.", nameof(thresholds));
    }

    public static readonly LevelTable Default = new(Enumerable.Range(1, 100).Select(l => 75m * l * (l - 1)));

    public int MaxLevel => _thresholds.Length;

    public LevelInfo For(decimal totalXp)
    {
        int index = Array.FindLastIndex(_thresholds, t => t <= totalXp);
        if (index < 0)
            index = 0;
        decimal? next = index + 1 < _thresholds.Length ? _thresholds[index + 1] : null;
        return new LevelInfo(index + 1, totalXp, _thresholds[index], next);
    }
}
