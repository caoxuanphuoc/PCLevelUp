namespace GrowthTracker.Domain.Common;

/// <summary>Thrown when an entity invariant or business rule (BR-xx / FR-xx) would be violated.</summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
