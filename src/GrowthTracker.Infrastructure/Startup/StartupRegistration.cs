using System.Runtime.Versioning;
using GrowthTracker.Application.Abstractions;
using Microsoft.Win32;

namespace GrowthTracker.Infrastructure.Startup;

/// <summary>
/// Start with the OS: the per-user Run key on Windows, an XDG autostart entry on Linux. The app is launched
/// with <see cref="MinimizedArgument"/> so it starts in the tray.
/// </summary>
public sealed class StartupRegistration(string executablePath, string autostartDirectory) : IStartupRegistration
{
    public const string MinimizedArgument = "--minimized";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GrowthTracker";

    public static StartupRegistration ForCurrentProcess()
    {
        string? xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string config = string.IsNullOrWhiteSpace(xdg)
            ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
        return new StartupRegistration(Environment.ProcessPath ?? "GrowthTracker.Desktop", System.IO.Path.Combine(config, "autostart"));
    }

    public string DesktopEntryPath => System.IO.Path.Combine(autostartDirectory, "growth-tracker.desktop");

    public bool IsRegistered => OperatingSystem.IsWindows() ? WindowsIsRegistered() : File.Exists(DesktopEntryPath);

    public bool TrySet(bool enabled, out string? error)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                WindowsSet(enabled);
            else
                LinuxSet(enabled);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            error = ex.Message;
            return false;
        }
    }

    public string LinuxDesktopEntry() =>
        $"""
        [Desktop Entry]
        Type=Application
        Name=Growth Tracker
        Comment=Personal Growth Tracker
        Exec="{executablePath}" {MinimizedArgument}
        Terminal=false
        X-GNOME-Autostart-enabled=true
        """;

    private void LinuxSet(bool enabled)
    {
        if (enabled)
        {
            Directory.CreateDirectory(autostartDirectory);
            File.WriteAllText(DesktopEntryPath, LinuxDesktopEntry() + Environment.NewLine);
        }
        else if (File.Exists(DesktopEntryPath))
        {
            File.Delete(DesktopEntryPath);
        }
    }

    [SupportedOSPlatform("windows")]
    private bool WindowsIsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    [SupportedOSPlatform("windows")]
    private void WindowsSet(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
            key.SetValue(ValueName, $"\"{executablePath}\" {MinimizedArgument}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
