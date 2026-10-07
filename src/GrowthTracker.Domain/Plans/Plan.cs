using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Plans;

public sealed class Plan : Entity
{
    private readonly List<PlanGoalLink> _goalLinks = [];
    private readonly List<AchievementCriterion> _criteria = [];

    private Plan() { }

    /// <param name="goals">Goals this plan contributes to, with weights; at least one (BR-01).</param>
    /// <param name="criteria">Criterion titles; required for <see cref="PlanType.Achievement"/>.</param>
    public Plan(string name, PlanType planType, decimal targetValue, string? targetUnit,
                DateOnly durationStart, DateOnly durationEnd, Difficulty difficulty,
                IEnumerable<(Guid GoalId, decimal Weight)> goals,
                WeekDays daysOfWeek = WeekDays.None, int? frequencyPerWeek = null,
                TimeOnly? preferredTime = null, Guid? milestoneId = null,
                IEnumerable<string>? criteria = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("plan.name_required", "Plan name is required.");
        if (durationEnd < durationStart)
            throw new DomainException("plan.duration", "Duration end must not be before start.");

        Name = name.Trim();
        PlanType = planType;
        TargetValue = targetValue;
        TargetUnit = targetUnit;
        DurationStart = durationStart;
        DurationEnd = durationEnd;
        Difficulty = difficulty;
        DaysOfWeek = daysOfWeek;
        FrequencyPerWeek = frequencyPerWeek;
        PreferredTime = preferredTime;
        MilestoneId = milestoneId;

        foreach (var (goalId, weight) in goals)
            _goalLinks.Add(new PlanGoalLink(Id, goalId, weight));
        foreach (var title in criteria ?? [])
            _criteria.Add(new AchievementCriterion(Id, title));

        EnsureMeasurable();
    }

    public Guid? MilestoneId { get; private set; }
    public string Name { get; private set; } = "";
    public PlanType PlanType { get; private set; }
    /// <summary>Minutes (Time), units (Quantity), sessions per week (Frequency); unused for Binary and Achievement.</summary>
    public decimal TargetValue { get; private set; }
    public string? TargetUnit { get; private set; }
    public int? FrequencyPerWeek { get; private set; }
    public WeekDays DaysOfWeek { get; private set; }
    public TimeOnly? PreferredTime { get; private set; }
    public DateOnly DurationStart { get; private set; }
    public DateOnly DurationEnd { get; private set; }
    public Difficulty Difficulty { get; private set; }
    public PlanStatus Status { get; private set; } = PlanStatus.Active;

    public IReadOnlyList<PlanGoalLink> GoalLinks => _goalLinks;
    public IReadOnlyList<AchievementCriterion> Criteria => _criteria;

    /// <summary>Applies a user-confirmed target change; only future sessions are affected (BR-14).</summary>
    /// <remarks>
    /// For Frequency plans the new number of sessions per week is spread across the week again (explicit days
    /// cannot keep their meaning when the count changes); callers reschedule the future sessions.
    /// </remarks>
    public void UpdateTarget(decimal newTarget)
    {
        if (PlanType == PlanType.Frequency)
        {
            FrequencyPerWeek = (int)newTarget;
            DaysOfWeek = WeekDays.None;
        }
        TargetValue = newTarget;
        EnsureMeasurable();
    }

    /// <summary>Archiving keeps earned XP (BR-07); the XP ledger is never touched here.</summary>
    public void Archive() => Status = PlanStatus.Archived;

    /// <summary>Moves the plan under another milestone of the same goal, or detaches it (null).</summary>
    public void MoveToMilestone(Guid? milestoneId) => MilestoneId = milestoneId;

    public Scheduling.ScheduleInput ToScheduleInput() => new(PlanType, DurationStart, DurationEnd, DaysOfWeek, FrequencyPerWeek);

    private void EnsureMeasurable()
    {
        if (_goalLinks.Count == 0)
            throw new DomainException("plan.goal_required", "A plan must belong to at least one goal (BR-01).");
        if (_goalLinks.Any(l => l.Weight <= 0))
            throw new DomainException("plan.goal_weight", "Goal link weights must be positive.");

        // BR-02: every plan needs a way to be measured.
        switch (PlanType)
        {
            case PlanType.Time or PlanType.Quantity when TargetValue <= 0:
                throw new DomainException("plan.target_required", "Time and quantity plans need a positive target.");
            case PlanType.Frequency when FrequencyPerWeek is not (>= 1 and <= 7):
                throw new DomainException("plan.frequency_required", "Frequency plans need 1 to 7 sessions per week.");
            case PlanType.Achievement when _criteria.Count == 0:
                throw new DomainException("plan.criteria_required", "Achievement plans need at least one criterion.");
        }
    }
}
