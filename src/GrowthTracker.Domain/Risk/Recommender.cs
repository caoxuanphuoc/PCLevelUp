using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;

namespace GrowthTracker.Domain.Risk;

/// <param name="CurrentTarget">Minutes (Time), units (Quantity) or sessions per week (Frequency).</param>
/// <param name="CompletionRatePct">Done ÷ due sessions over the recommendation window.</param>
/// <param name="ConsistencyPct">Consistency over the same window.</param>
/// <param name="LastDismissedOn">When the user last dismissed a suggestion for this plan.</param>
public sealed record RecommendationInput(
    PlanType PlanType,
    decimal CurrentTarget,
    decimal CompletionRatePct,
    decimal ConsistencyPct,
    int DaysOfData,
    DateOnly Today,
    DateOnly? LastDismissedOn = null);

public sealed record TargetSuggestion(RecommendationKind Kind, decimal CurrentValue, decimal SuggestedValue, string Reason);

/// <summary>
/// Rule-based target suggestions (FR-52, FR-53). Nothing is applied automatically (FR-54).
/// Decrease is checked first: a plan the user cannot keep up with should get lighter before anything else.
/// </summary>
public sealed class Recommender(DomainThresholds thresholds)
{
    public Recommender() : this(DomainThresholds.Default) { }

    public TargetSuggestion? Suggest(RecommendationInput input)
    {
        if (input.PlanType is PlanType.Binary or PlanType.Achievement || input.CurrentTarget <= 0)
            return null;
        if (input.DaysOfData < thresholds.RecommendationWindowWeeks * 7)
            return null; // BR-13: suggestions need 4 weeks of data
        if (input.LastDismissedOn is DateOnly dismissed
            && input.Today < dismissed.AddDays(thresholds.RecommendationSnoozeWeeks * 7))
            return null; // FR-54: hidden for 4 weeks after dismissal

        if (input.CompletionRatePct < thresholds.DecreaseTargetBelowPct)
        {
            decimal suggested = Decrease(input.PlanType, input.CurrentTarget, input.CompletionRatePct);
            return suggested < input.CurrentTarget
                ? new TargetSuggestion(RecommendationKind.DecreaseTarget, input.CurrentTarget, suggested,
                    $"Completion {input.CompletionRatePct:0}% in the last {thresholds.RecommendationWindowWeeks} weeks")
                : null;
        }

        if (input.ConsistencyPct >= thresholds.IncreaseTargetAtOrAbovePct)
        {
            decimal suggested = Increase(input.PlanType, input.CurrentTarget);
            return suggested > input.CurrentTarget
                ? new TargetSuggestion(RecommendationKind.IncreaseTarget, input.CurrentTarget, suggested,
                    $"Consistency {input.ConsistencyPct:0}% in the last {thresholds.RecommendationWindowWeeks} weeks")
                : null;
        }

        return null;
    }

    /// <summary>Scale the target to what was actually achieved, rounded up (Gym 5/week at 48% → 3).</summary>
    private static decimal Decrease(PlanType type, decimal current, decimal completionPct)
    {
        decimal step = type == PlanType.Time ? 5m : 1m;
        decimal scaled = current * completionPct / 100m;
        decimal suggested = Math.Max(step, type == PlanType.Time ? RoundUpTo(scaled, step) : Math.Ceiling(scaled));
        return suggested >= current ? Math.Max(step, current - step) : suggested;
    }

    /// <summary>Time +50% rounded to 5 min (20 → 30), Quantity +25% rounded up, Frequency +1 session (max 7).</summary>
    private static decimal Increase(PlanType type, decimal current) => type switch
    {
        PlanType.Time => RoundUpTo(current * 1.5m, 5m),
        PlanType.Quantity => Math.Ceiling(current * 1.25m),
        PlanType.Frequency => Math.Min(7m, current + 1m),
        _ => current,
    };

    private static decimal RoundUpTo(decimal value, decimal step) => Math.Ceiling(value / step) * step;
}
