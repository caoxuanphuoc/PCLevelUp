namespace GrowthTracker.Application.Abstractions;

/// <summary>
/// OS notification. Windows shows it through the app's notify icon (rendered as a toast on Windows 10/11);
/// Linux uses org.freedesktop.Notifications over D-Bus. Implementations never throw: a missing notification
/// service must not break the app.
/// </summary>
public interface INotifier
{
    Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    /// <summary>Copies the live database with the SQLite Online Backup API; returns the backup path, or null if there is no database yet.</summary>
    Task<string?> BackupAsync(string reason, CancellationToken cancellationToken = default);
}

/// <summary>Registers the app to start with the OS (Windows Run key, Linux XDG autostart entry).</summary>
public interface IStartupRegistration
{
    bool IsRegistered { get; }

    bool TrySet(bool enabled, out string? error);
}
