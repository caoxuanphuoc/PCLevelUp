using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Goals;

public sealed class Goal : Entity
{
    private Goal() { }

    public Goal(Guid areaId, string name, DateOnly startDate, DateOnly targetDate, int priority,
                string? description = null, string? targetDescription = null, Guid? visionId = null)
    {
        Update(areaId, name, startDate, targetDate, priority, description, targetDescription);
        VisionId = visionId;
    }

    public void Update(Guid areaId, string name, DateOnly startDate, DateOnly targetDate, int priority,
                       string? description, string? targetDescription)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("goal.name_required", "Goal name is required.");
        if (targetDate <= startDate)
            throw new DomainException("goal.dates", "Target date must be after start date.");
        if (priority is < 1 or > 4)
            throw new DomainException("goal.priority", "Priority must be between 1 and 4.");

        AreaId = areaId;
        Name = name.Trim();
        StartDate = startDate;
        TargetDate = targetDate;
        Priority = priority;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        TargetDescription = string.IsNullOrWhiteSpace(targetDescription) ? null : targetDescription.Trim();
    }

    public Guid AreaId { get; private set; }
    public Guid? VisionId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    /// <summary>1 = highest priority, 4 = lowest.</summary>
    public int Priority { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly TargetDate { get; private set; }
    public string? TargetDescription { get; private set; }
    public GoalStatus Status { get; private set; } = GoalStatus.Draft;

    // Cached values; always recomputable from logs (TDD §2.1).
    public decimal Progress { get; private set; }
    public decimal ExpectedProgress { get; private set; }
    public decimal? Score { get; private set; }

    public bool IsClosed => Status is GoalStatus.Completed or GoalStatus.Cancelled;

    /// <summary>Called by the system when the first Plan of this Goal is scheduled.</summary>
    public void Activate()
    {
        if (Status == GoalStatus.Draft)
            Status = GoalStatus.Active;
    }

    /// <summary>Applies freshly computed progress; moves between On Track / At Risk once enough data exists.</summary>
    public void ApplyProgress(decimal progress, decimal expectedProgress, decimal? score, bool? isAtRisk)
    {
        Progress = progress;
        ExpectedProgress = expectedProgress;
        Score = score;
        if (IsClosed || Status == GoalStatus.Draft || isAtRisk is null)
            return;
        Status = isAtRisk.Value ? GoalStatus.AtRisk : GoalStatus.OnTrack;
    }

    /// <summary>System proposes completion, the user confirms it.</summary>
    public void Complete()
    {
        if (Status == GoalStatus.Cancelled)
            throw new DomainException("goal.cancelled", "A cancelled goal cannot be completed.");
        Status = GoalStatus.Completed;
    }

    public void Cancel() => Status = GoalStatus.Cancelled;
}
