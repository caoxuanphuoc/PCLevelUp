using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Domain.Scoring;

/// <param name="Actual">Minutes or units done (Time / Quantity).</param>
/// <param name="Target">Target per session (Time / Quantity).</param>
/// <param name="Done">Whether the session was done (Binary / Frequency).</param>
/// <param name="Quality">Self-rated stars, 1–5.</param>
/// <param name="SessionsDoneLast7Days">Done sessions in the rolling 7-day window, including this one.</param>
/// <param name="SessionsTargetLast7Days">Scheduled sessions in the same window; skipped ones still count (BR-10).</param>
public sealed record ScoreInput(
    PlanType PlanType,
    Difficulty Difficulty,
    int Quality,
    int SessionsDoneLast7Days,
    int SessionsTargetLast7Days,
    decimal Actual = 0,
    decimal Target = 0,
    bool Done = false,
    int CriteriaDone = 0,
    int CriteriaTotal = 0);

/// <summary>All components in 0..100, the capped integer Score and the XP it earns.</summary>
public sealed record ScoreBreakdown(
    decimal Completion,
    decimal Quality,
    decimal Consistency,
    decimal DifficultyPoints,
    int Score,
    decimal Xp,
    int ScoreVersion);

/// <summary>Pure Score calculation (BRD §5.3, TDD §5.1). Users never enter Score directly (BR-03).</summary>
public sealed class ScoreEngine(ScoreWeights weights, DifficultyTable difficulty)
{
    public ScoreEngine() : this(ScoreWeights.Default, DifficultyTable.Default) { }

    public ScoreWeights Weights => weights;

    public ScoreBreakdown Calculate(ScoreInput input)
    {
        if (input.Quality is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(input), input.Quality, "Quality must be 1–5 stars.");

        decimal c = Completion(input);
        decimal q = input.Quality / 5m * 100m;
        decimal s = Consistency(input.SessionsDoneLast7Days, input.SessionsTargetLast7Days);
        decimal d = difficulty.Points(input.Difficulty);

        decimal raw = c * weights.Completion + q * weights.Quality
                    + s * weights.Consistency + d * weights.Difficulty;
        int score = (int)Math.Round(Math.Min(100m, raw), MidpointRounding.AwayFromZero); // BR-05
        decimal xp = score * difficulty.XpMultiplier(input.Difficulty);                   // BR-06

        return new ScoreBreakdown(c, q, s, d, score, xp, weights.Version);
    }

    /// <summary>Per-session completion, capped at 100: overshooting is recorded but earns nothing extra (BR-09).</summary>
    public static decimal Completion(ScoreInput i) => i.PlanType switch
    {
        PlanType.Time or PlanType.Quantity =>
            i.Target <= 0 ? 0m : Math.Min(1m, Math.Max(0m, i.Actual) / i.Target) * 100m,
        PlanType.Binary or PlanType.Frequency => i.Done ? 100m : 0m,
        PlanType.Achievement =>
            i.CriteriaTotal <= 0 ? 0m : Math.Min(1m, i.CriteriaDone / (decimal)i.CriteriaTotal) * 100m,
        _ => throw new ArgumentOutOfRangeException(nameof(i), i.PlanType, "Unknown plan type."),
    };

    public static decimal Consistency(int done, int target) =>
        target <= 0 ? (done > 0 ? 100m : 0m) : Math.Min(100m, done / (decimal)target * 100m);
}
