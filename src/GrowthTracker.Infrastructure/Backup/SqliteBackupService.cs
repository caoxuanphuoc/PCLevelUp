using System.Globalization;
using GrowthTracker.Application.Abstractions;
using Microsoft.Data.Sqlite;

namespace GrowthTracker.Infrastructure.Backup;

/// <summary>
/// Backs up the live database with the SQLite Online Backup API (safe while the app is writing),
/// keeping the newest backup of each of the last 7 days plus each of the last 4 weeks.
/// </summary>
public sealed class SqliteBackupService(AppPaths paths, IClock clock) : IBackupService
{
    private const string Prefix = "growth-";
    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    public Task<string?> BackupAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.DatabasePath) || new FileInfo(paths.DatabasePath).Length == 0)
            return Task.FromResult<string?>(null);

        Directory.CreateDirectory(paths.BackupDirectory);
        string stamp = clock.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        string destination = Path.Combine(paths.BackupDirectory, $"{Prefix}{stamp}-{reason}.db");

        using (var source = new SqliteConnection($"Data Source={paths.DatabasePath};Mode=ReadOnly;Pooling=False"))
        using (var target = new SqliteConnection($"Data Source={destination};Pooling=False"))
        {
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }

        Prune();
        return Task.FromResult<string?>(destination);
    }

    private void Prune()
    {
        var backups = Directory.EnumerateFiles(paths.BackupDirectory, $"{Prefix}*.db")
            .Select(path => (Path: path, Taken: ParseTimestamp(path)))
            .Where(b => b.Taken is not null)
            .Select(b => (b.Path, Taken: b.Taken!.Value));

        foreach (var path in SelectForDeletion(backups))
            File.Delete(path);
    }

    internal static IReadOnlyList<string> SelectForDeletion(IEnumerable<(string Path, DateTime Taken)> backups,
                                                            int keepDaily = 7, int keepWeekly = 4)
    {
        var newestFirst = backups.OrderByDescending(b => b.Taken).ToList();

        var keep = new HashSet<string>();
        foreach (var path in newestFirst.GroupBy(b => b.Taken.Date).Take(keepDaily).Select(g => g.First().Path))
            keep.Add(path);
        foreach (var path in newestFirst.GroupBy(b => (ISOWeek.GetYear(b.Taken), ISOWeek.GetWeekOfYear(b.Taken)))
                                        .Take(keepWeekly).Select(g => g.First().Path))
            keep.Add(path);

        return newestFirst.Where(b => !keep.Contains(b.Path)).Select(b => b.Path).ToList();
    }

    private static DateTime? ParseTimestamp(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.Length < Prefix.Length + TimestampFormat.Length)
            return null;
        return DateTime.TryParseExact(name.AsSpan(Prefix.Length, TimestampFormat.Length), TimestampFormat,
                                      CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                      out var taken)
            ? taken
            : null;
    }
}
