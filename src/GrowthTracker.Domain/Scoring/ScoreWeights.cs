namespace GrowthTracker.Domain.Scoring;

/// <summary>
/// Versioned Score weights. Each PlanLog stores the version it was scored with so history can be recomputed.
/// </summary>
public sealed record ScoreWeights(int Version, decimal Completion, decimal Quality, decimal Consistency, decimal Difficulty)
{
    /// <summary>Original BRD formula: 40 / 25 / 20 / 15.</summary>
    public static readonly ScoreWeights V1 = new(1, 0.40m, 0.25m, 0.20m, 0.15m);

    /// <summary>BRD §9.3 proposal: Difficulty only multiplies XP, so an Easy plan done perfectly scores 100.</summary>
    public static readonly ScoreWeights V2 = new(2, 0.45m, 0.30m, 0.25m, 0m);

    public static readonly ScoreWeights Default = V2;

    public static ScoreWeights FromVersion(int version) => version switch
    {
        1 => V1,
        2 => V2,
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown score weights version."),
    };
}
