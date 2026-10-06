using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Areas;

public sealed class Area : Entity
{
    /// <summary>Six sample Areas created on first run (FR-01); colors from UI Design §2.1.</summary>
    public static readonly IReadOnlyList<(string Name, string ColorHex)> Defaults =
    [
        ("Career", "#2563EB"),
        ("English", "#7C3AED"),
        ("Health", "#16A34A"),
        ("Finance", "#EA580C"),
        ("Knowledge", "#0D9488"),
        ("Personal", "#DB2777"),
    ];

    private Area() { }

    public Area(string name, string colorHex, int sortOrder)
    {
        Rename(name);
        ColorHex = colorHex;
        SortOrder = sortOrder;
    }

    /// <summary>Used by seeding so default Areas keep stable ids across databases.</summary>
    public Area(Guid id, string name, string colorHex, int sortOrder) : this(name, colorHex, sortOrder) => Id = id;

    public string Name { get; private set; } = "";
    public string ColorHex { get; private set; } = "";
    public int SortOrder { get; private set; }
    public bool IsArchived { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("area.name_required", "Area name is required.");
        Name = name.Trim();
    }

    public void Archive() => IsArchived = true;
}
