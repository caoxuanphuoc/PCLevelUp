namespace GrowthTracker.Domain.Risk;

public sealed record GoalRisk(bool IsAtRisk, decimal GapPoints);

public sealed record PlanRisk(bool IsTooHard, decimal ConsistencyPct);

public sealed class RiskDetector(DomainThresholds thresholds)
{
    public RiskDetector() : this(DomainThresholds.Default) { }

    /// <summary>FR-50: a Goal is At Risk when actual progress trails expected by more than the gap (10 points).</summary>
    public GoalRisk EvaluateGoal(decimal actualProgress, decimal expectedProgress)
    {
        decimal gap = expectedProgress - actualProgress;
        return new GoalRisk(gap > thresholds.AtRiskGapPoints, gap);
    }

    /// <summary>
    /// FR-51: a Plan may be too hard when Consistency over the last 14 days is below 50%.
    /// Needs at least <see cref="DomainThresholds.MinDaysForTrends"/> days of data (BR-13).
    /// </summary>
    public PlanRisk EvaluatePlan(int sessionsDone, int sessionsScheduled, int daysOfData)
    {
        if (sessionsScheduled <= 0)
            return new PlanRisk(false, 0m);
        decimal consistency = Math.Min(100m, sessionsDone * 100m / sessionsScheduled);
        bool enoughData = daysOfData >= thresholds.MinDaysForTrends;
        return new PlanRisk(enoughData && consistency < thresholds.PlanTooHardConsistencyPct, consistency);
    }
}
