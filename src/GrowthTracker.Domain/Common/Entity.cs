namespace GrowthTracker.Domain.Common;

/// <summary>
/// Base for every persisted entity: GUID v7 key, audit timestamps and soft delete (sync-ready, TDD §2.1).
/// CreatedAt/UpdatedAt are stamped by the persistence layer.
/// </summary>
public abstract class Entity
{
    private readonly List<DomainEvent> _domainEvents = [];

    public Guid Id { get; protected init; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; private set; }

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents;

    public void SoftDelete() => IsDeleted = true;

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
