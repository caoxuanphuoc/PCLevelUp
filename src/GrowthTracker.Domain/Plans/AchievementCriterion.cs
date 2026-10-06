using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Plans;

public sealed class AchievementCriterion : Entity
{
    private AchievementCriterion() { }

    public AchievementCriterion(Guid planId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("criterion.title_required", "Criterion title is required.");
        PlanId = planId;
        Title = title.Trim();
    }

    public Guid PlanId { get; private set; }
    public string Title { get; private set; } = "";
    public bool IsDone { get; private set; }
    public DateTime? DoneAt { get; private set; }

    public void SetDone(bool done, DateTime nowUtc)
    {
        IsDone = done;
        DoneAt = done ? nowUtc : null;
    }
}
