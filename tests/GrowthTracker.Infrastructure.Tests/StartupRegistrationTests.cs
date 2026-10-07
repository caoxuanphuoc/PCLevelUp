using GrowthTracker.Infrastructure.Startup;

namespace GrowthTracker.Infrastructure.Tests;

public class StartupRegistrationTests
{
    [Fact]
    public void LinuxDesktopEntry_LaunchesMinimized()
    {
        var registration = new StartupRegistration("/opt/growth-tracker/GrowthTracker.Desktop", "/tmp/autostart");

        var entry = registration.LinuxDesktopEntry();

        entry.Should().Contain("[Desktop Entry]");
        entry.Should().Contain("Exec=\"/opt/growth-tracker/GrowthTracker.Desktop\" --minimized");
        registration.DesktopEntryPath.Should().Be(Path.Combine("/tmp/autostart", "growth-tracker.desktop"));
    }

    [Fact]
    public void Linux_TogglesAutostartFile()
    {
        if (OperatingSystem.IsWindows())
            return; // the Windows branch writes the real HKCU Run key, so it is only checked manually

        string dir = Path.Combine(Path.GetTempPath(), "gt-autostart-" + Guid.NewGuid().ToString("N"));
        var registration = new StartupRegistration("/usr/bin/growth-tracker", dir);
        try
        {
            registration.TrySet(true, out var error).Should().BeTrue();
            error.Should().BeNull();
            registration.IsRegistered.Should().BeTrue();

            registration.TrySet(false, out _).Should().BeTrue();
            registration.IsRegistered.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
