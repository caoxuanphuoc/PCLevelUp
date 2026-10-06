using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Plans;

public enum OccurrenceState
{
    Scheduled,
    Done,
    Skipped,
    Missed,
}

public enum SkipReason
{
    Busy,
    Sick,
    NotInTheMood,
    Other,
}

public sealed record PlanRescheduled(Guid OccurrenceId, Guid PlanId, DateOnly From, DateOnly To, DateTime OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

/// <summary>One scheduled session of a Plan. Kept separate from PlanLog, the result of that session.</summary>
public sealed class PlanOccurrence : Entity
{
    private PlanOccurrence() { }

    public PlanOccurrence(Guid planId, DateOnly scheduledDate, TimeOnly? scheduledTime = null)
    {
        PlanId = planId;
        ScheduledDate = scheduledDate;
        OriginalDate = scheduledDate;
        ScheduledTime = scheduledTime;
    }

    public Guid PlanId { get; private set; }
    public DateOnly ScheduledDate { get; private set; }
    public TimeOnly? ScheduledTime { get; private set; }
    public DateOnly OriginalDate { get; private set; }
    public OccurrenceState State { get; private set; } = OccurrenceState.Scheduled;
    public int RescheduleCount { get; private set; }
    public string? RescheduleReason { get; private set; }
    public SkipReason? SkipReason { get; private set; }

    /// <summary>Moves the session; never into the past (FR-24) and never changes Score (BR-12).</summary>
    public void Reschedule(DateOnly newDate, DateOnly today, string? reason, DateTime nowUtc, TimeOnly? newTime = null)
    {
        if (State != OccurrenceState.Scheduled)
            throw new DomainException("occurrence.not_scheduled", "Only scheduled sessions can be moved.");
        if (newDate < today)
            throw new DomainException("occurrence.past_date", "Sessions cannot be scheduled in the past (FR-24).");
        if (newDate == ScheduledDate && (newTime is null || newTime == ScheduledTime))
            return;

        var from = ScheduledDate;
        ScheduledDate = newDate;
        ScheduledTime = newTime ?? ScheduledTime;
        RescheduleCount++;
        RescheduleReason = reason;
        Raise(new PlanRescheduled(Id, PlanId, from, newDate, nowUtc));
    }

    internal void MarkDone() => State = OccurrenceState.Done;

    /// <summary>Skipped sessions have no Score but still count toward Consistency (BR-10).</summary>
    public void Skip(SkipReason reason, DateOnly today)
    {
        if (ScheduledDate > today)
            throw new DomainException("occurrence.future_date", "Future sessions cannot be skipped yet.");
        if (State == OccurrenceState.Done)
            throw new DomainException("occurrence.already_done", "A completed session cannot be skipped.");
        State = OccurrenceState.Skipped;
        SkipReason = reason;
    }

    /// <summary>Rollover job: a past session without a result becomes Missed.</summary>
    public void MarkMissedIfOverdue(DateOnly today)
    {
        if (State == OccurrenceState.Scheduled && ScheduledDate < today)
            State = OccurrenceState.Missed;
    }
}
