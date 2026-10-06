using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Reviews;

public enum RecommendationKind
{
    DecreaseTarget,
    IncreaseTarget,
}

public enum RecommendationState
{
    Pending,
    Applied,
    Dismissed,
}

/// <summary>A target-change suggestion; it only takes effect once the user confirms it (FR-54, BR-14).</summary>
public sealed class Recommendation : Entity
{
    private Recommendation() { }

    public Recommendation(Guid planId, RecommendationKind kind, decimal currentValue, decimal suggestedValue,
                          string reason, Guid? reviewId = null)
    {
        PlanId = planId;
        Kind = kind;
        CurrentValue = currentValue;
        SuggestedValue = suggestedValue;
        Reason = reason;
        ReviewId = reviewId;
    }

    public Guid? ReviewId { get; private set; }
    public Guid PlanId { get; private set; }
    public RecommendationKind Kind { get; private set; }
    public decimal CurrentValue { get; private set; }
    public decimal SuggestedValue { get; private set; }
    public string Reason { get; private set; } = "";
    public RecommendationState State { get; private set; }
    public DateOnly? SnoozedUntil { get; private set; }

    public void MarkApplied() => State = RecommendationState.Applied;

    /// <summary>A dismissed suggestion stays hidden for the snooze period (4 weeks by default).</summary>
    public void Dismiss(DateOnly today, int snoozeWeeks)
    {
        State = RecommendationState.Dismissed;
        SnoozedUntil = today.AddDays(7 * snoozeWeeks);
    }
}
