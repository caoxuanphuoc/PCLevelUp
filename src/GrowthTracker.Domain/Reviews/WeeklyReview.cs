using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Reviews;

public sealed class WeeklyReview : Entity
{
    private WeeklyReview() { }

    public WeeklyReview(DateOnly weekStart, int score, int plansDone, int plansTotal, decimal consistency,
                        decimal xpGained, string areaSnapshotJson)
    {
        WeekStart = weekStart;
        Score = score;
        PlansDone = plansDone;
        PlansTotal = plansTotal;
        Consistency = consistency;
        XpGained = xpGained;
        AreaSnapshotJson = areaSnapshotJson;
    }

    public DateOnly WeekStart { get; private set; }
    public int Score { get; private set; }
    public int PlansDone { get; private set; }
    public int PlansTotal { get; private set; }
    public decimal Consistency { get; private set; }
    public decimal XpGained { get; private set; }
    public string? Note { get; private set; }
    /// <summary>JSON snapshot of per-Area figures for the week.</summary>
    public string AreaSnapshotJson { get; private set; } = "{}";
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Updates the snapshot when the report is regenerated (e.g. a log edited within 24 hours).</summary>
    public void Refresh(int score, int plansDone, int plansTotal, decimal consistency, decimal xpGained, string areaSnapshotJson)
    {
        Score = score;
        PlansDone = plansDone;
        PlansTotal = plansTotal;
        Consistency = consistency;
        XpGained = xpGained;
        AreaSnapshotJson = areaSnapshotJson;
    }

    public void SaveNote(string? note) => Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    /// <summary>"Hoàn tất review" (FR-43): marks it seen, which also hides the Dashboard reminder.</summary>
    public void Complete(string? note, DateTime nowUtc)
    {
        SaveNote(note);
        CompletedAt = nowUtc;
    }

    public bool IsCompleted => CompletedAt is not null;
}
