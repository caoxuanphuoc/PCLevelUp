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

/// <summary>
/// Auto-update from GitHub Releases (TDD §8.3), implemented with Velopack. Updates only work in an installed
/// build; a dev run or a test reports <see cref="IsSupported"/> = false and never touches the network.
/// </summary>
public interface IUpdateService
{
    bool IsSupported { get; }

    /// <summary>The running version, e.g. "1.0.0".</summary>
    string CurrentVersion { get; }

    /// <summary>Returns the newer version on the feed, or null when up to date. Beta builds are GitHub pre-releases.</summary>
    Task<string?> CheckAsync(bool includePrerelease, CancellationToken cancellationToken = default);

    /// <summary>Downloads the update found by the last <see cref="CheckAsync"/> (a delta package when possible).</summary>
    Task DownloadAsync(Action<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Hands the downloaded update to the updater, which applies it and restarts the app once this process exits.</summary>
    void ApplyOnExitAndRestart();
}
