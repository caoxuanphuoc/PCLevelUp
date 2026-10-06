using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Goals;

/// <summary>Long-term direction; descriptive only in the MVP (no progress).</summary>
public sealed class Vision : Entity
{
    private Vision() { }

    public Vision(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("vision.title_required", "Vision title is required.");
        Title = title.Trim();
        Description = description;
    }

    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
}
