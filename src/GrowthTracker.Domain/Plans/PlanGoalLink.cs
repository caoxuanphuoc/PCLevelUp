namespace GrowthTracker.Domain.Plans;

/// <summary>Many-to-many Plan ↔ Goal with a contribution weight (BR-01).</summary>
public sealed class PlanGoalLink
{
    private PlanGoalLink() { }

    public PlanGoalLink(Guid planId, Guid goalId, decimal weight = 1m)
    {
        PlanId = planId;
        GoalId = goalId;
        Weight = weight;
    }

    public Guid PlanId { get; private set; }
    public Guid GoalId { get; private set; }
    public decimal Weight { get; private set; }
}
