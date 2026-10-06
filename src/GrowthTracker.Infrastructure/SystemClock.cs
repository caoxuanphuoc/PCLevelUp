using GrowthTracker.Application.Abstractions;

namespace GrowthTracker.Infrastructure;

public sealed class SystemClock(TimeZoneInfo timeZone) : IClock
{
    public SystemClock() : this(TimeZoneInfo.Local) { }

    public DateTime UtcNow => DateTime.UtcNow;

    public TimeZoneInfo TimeZone => timeZone;
}
