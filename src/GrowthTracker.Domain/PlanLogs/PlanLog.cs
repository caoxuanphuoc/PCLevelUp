using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Scoring;

namespace GrowthTracker.Domain.PlanLogs;

public sealed record PlanLogged(Guid PlanLogId, Guid OccurrenceId, Guid PlanId, int Score, decimal Xp, DateTime OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

/// <summary>Result of one session. Stores every score component for display and audit.</summary>
public sealed class PlanLog : Entity
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromHours(24);

    private PlanLog() { }

    private PlanLog(Guid occurrenceId) => OccurrenceId = occurrenceId;

    public Guid OccurrenceId { get; private set; }
    public decimal ActualValue { get; private set; }
    public int Quality { get; private set; }
    public string? Note { get; private set; }
    public decimal Completion { get; private set; }
    public decimal QualityPct { get; private set; }
    public decimal Consistency { get; private set; }
    public decimal DifficultyPts { get; private set; }
    public int Score { get; private set; }
    public decimal Xp { get; private set; }
    public int ScoreVersion { get; private set; }
    public DateTime LoggedAt { get; private set; }
    public DateTime EditableUntil { get; private set; }

    public static PlanLog Record(PlanOccurrence occurrence, decimal actualValue, int quality, string? note,
                                 ScoreBreakdown breakdown, DateOnly today, DateTime nowUtc)
    {
        if (occurrence.ScheduledDate > today)
            throw new DomainException("planlog.future_date", "Results cannot be logged for a future date (FR-24).");
        if (occurrence.State == OccurrenceState.Done)
            throw new DomainException("planlog.already_logged", "This session already has a result.");

        var log = new PlanLog(occurrence.Id) { LoggedAt = nowUtc, EditableUntil = nowUtc + EditWindow };
        log.Apply(actualValue, quality, note, breakdown);
        occurrence.MarkDone();
        log.Raise(new PlanLogged(log.Id, occurrence.Id, occurrence.PlanId, log.Score, log.Xp, nowUtc));
        return log;
    }

    public bool CanEdit(DateTime nowUtc) => nowUtc <= EditableUntil;

    /// <summary>Edits are allowed for 24 hours after logging; the Score is recomputed (BR-11, FR-33).</summary>
    public void Edit(decimal actualValue, int quality, string? note, ScoreBreakdown breakdown, DateTime nowUtc)
    {
        if (!CanEdit(nowUtc))
            throw new DomainException("planlog.edit_window", "Plan logs can only be edited within 24 hours (BR-11).");
        Apply(actualValue, quality, note, breakdown);
    }

    private void Apply(decimal actualValue, int quality, string? note, ScoreBreakdown b)
    {
        if (quality is < 1 or > 5)
            throw new DomainException("planlog.quality", "Quality must be 1 to 5 stars.");
        ActualValue = actualValue;
        Quality = quality;
        Note = note;
        Completion = b.Completion;
        QualityPct = b.Quality;
        Consistency = b.Consistency;
        DifficultyPts = b.DifficultyPoints;
        Score = b.Score;
        Xp = b.Xp;
        ScoreVersion = b.ScoreVersion;
    }
}
