using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Milestones;

public enum MilestoneStatus
{
    Open,
    Completed,
}

public sealed class Milestone : Entity
{
    private Milestone() { }

    public Milestone(Guid goalId, string name, int sortOrder, decimal weight = 1m, DateOnly? dueDate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("milestone.name_required", "Milestone name is required.");
        if (weight <= 0)
            throw new DomainException("milestone.weight", "Milestone weight must be positive.");
        GoalId = goalId;
        Name = name.Trim();
        SortOrder = sortOrder;
        Weight = weight;
        DueDate = dueDate;
    }

    public Guid GoalId { get; private set; }
    public string Name { get; private set; } = "";
    public DateOnly? DueDate { get; private set; }
    /// <summary>Relative weight within the Goal; equal by default (FR-03).</summary>
    public decimal Weight { get; private set; }
    public int SortOrder { get; private set; }
    public MilestoneStatus Status { get; private set; }
    public decimal Progress { get; private set; }

    public void ApplyProgress(decimal progress)
    {
        Progress = progress;
        if (progress >= 100m)
            Status = MilestoneStatus.Completed;
    }
}
