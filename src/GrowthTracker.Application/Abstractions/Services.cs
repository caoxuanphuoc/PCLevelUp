namespace GrowthTracker.Application.Abstractions;

/// <summary>OS notification (Windows toast / Linux D-Bus). Implementations arrive with reminders in Sprint 4.</summary>
public interface INotifier
{
    Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    /// <summary>Copies the live database with the SQLite Online Backup API; returns the backup path, or null if there is no database yet.</summary>
    Task<string?> BackupAsync(string reason, CancellationToken cancellationToken = default);
}
