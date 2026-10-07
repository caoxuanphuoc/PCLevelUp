namespace GrowthTracker.Domain.Risk;

/// <summary>
/// Risk, recommendation and trend thresholds (BRD §5.9, BR-13, FR-42). All are initial proposals awaiting sign-off,
/// so they live in one place.
/// </summary>
public sealed record DomainThresholds(
    decimal AtRiskGapPoints = 10m,
    decimal PlanTooHardConsistencyPct = 50m,
    int PlanRiskWindowDays = 14,
    decimal DecreaseTargetBelowPct = 60m,
    decimal IncreaseTargetAtOrAbovePct = 85m,
    int RecommendationWindowWeeks = 4,
    int RecommendationSnoozeWeeks = 4,
    int MinDaysForTrends = 7,
    decimal TrendThresholdPoints = 3m)
{
    public static readonly DomainThresholds Default = new();
}
