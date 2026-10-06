using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.Areas;

/// <summary>Single local profile in the MVP (TDD §7.4).</summary>
public sealed class UserProfile : Entity
{
    private UserProfile() { }

    public UserProfile(string displayName, string locale, string timeZone, DayOfWeek weekStartsOn = DayOfWeek.Monday)
    {
        DisplayName = displayName;
        Locale = locale;
        TimeZone = timeZone;
        WeekStartsOn = weekStartsOn;
    }

    public string DisplayName { get; private set; } = "";
    public string? PinHash { get; private set; }
    public string Locale { get; private set; } = "vi-VN";
    public string TimeZone { get; private set; } = "UTC";
    public DayOfWeek WeekStartsOn { get; private set; } = DayOfWeek.Monday;

    public void SetPinHash(string? pinHash) => PinHash = pinHash;
}
