namespace GrowthTracker.Application.Abstractions;

/// <summary>
/// The only source of time for the app. Instants are UTC; business "days" are DateOnly in the user's time zone.
/// Faked in tests to cover day/week rollovers and the 24-hour edit rule.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }

    TimeZoneInfo TimeZone { get; }

    DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZone));
}
