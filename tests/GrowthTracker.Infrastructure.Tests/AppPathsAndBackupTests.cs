using GrowthTracker.Infrastructure.Backup;
using Microsoft.Data.Sqlite;

namespace GrowthTracker.Infrastructure.Tests;

public class AppPathsTests
{
    [Fact]
    public void Windows_UsesLocalAndRoamingAppData()
    {
        var env = new Dictionary<string, string?> { ["LOCALAPPDATA"] = @"C:\L", ["APPDATA"] = @"C:\R" };

        var paths = AppPaths.Resolve(isWindows: true, k => env.GetValueOrDefault(k), @"C:\Users\me");

        paths.DatabasePath.Should().Be(Path.Combine(@"C:\L", "GrowthTracker", "growth.db"));
        paths.BackupDirectory.Should().Be(Path.Combine(@"C:\L", "GrowthTracker", "backups"));
        paths.LogDirectory.Should().Be(Path.Combine(@"C:\L", "GrowthTracker", "logs"));
        paths.SettingsPath.Should().Be(Path.Combine(@"C:\R", "GrowthTracker", "settings.json"));
    }

    [Fact]
    public void Linux_DefaultsToXdgFallbacks()
    {
        var paths = AppPaths.Resolve(isWindows: false, _ => null, "/home/me");

        paths.DatabasePath.Should().Be(Path.Combine("/home/me", ".local", "share", "growth-tracker", "growth.db"));
        paths.LogDirectory.Should().Be(Path.Combine("/home/me", ".local", "state", "growth-tracker", "logs"));
        paths.SettingsPath.Should().Be(Path.Combine("/home/me", ".config", "growth-tracker", "settings.json"));
    }

    [Fact]
    public void Linux_HonoursXdgVariables()
    {
        var paths = AppPaths.Resolve(false, k => k == "XDG_DATA_HOME" ? "/data" : null, "/home/me");

        paths.DatabasePath.Should().Be(Path.Combine("/data", "growth-tracker", "growth.db"));
    }

    [Fact]
    public void ForCurrentOs_MatchesPlatform()
    {
        var paths = AppPaths.ForCurrentOs();

        paths.DatabasePath.Should().Contain(OperatingSystem.IsWindows() ? "GrowthTracker" : "growth-tracker");
    }
}

public class BackupTests
{
    [Fact]
    public void Retention_KeepsNewestPerDayForSevenDays_AndPerWeekForFourWeeks()
    {
        var start = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        // Two backups a day for 60 days.
        var backups = Enumerable.Range(0, 60)
            .SelectMany(d => new[] { start.AddDays(d), start.AddDays(d).AddHours(5) })
            .Select(t => ($"b-{t:yyyyMMddHH}", t))
            .ToList();

        var deleted = SqliteBackupService.SelectForDeletion(backups);
        var kept = backups.Select(b => b.Item1).Except(deleted).ToList();

        kept.Should().HaveCountGreaterThanOrEqualTo(7).And.HaveCountLessThanOrEqualTo(11);
        kept.Should().Contain($"b-{start.AddDays(59).AddHours(5):yyyyMMddHH}"); // newest
        kept.Should().NotContain($"b-{start.AddDays(59):yyyyMMddHH}");          // older one of the same day
        kept.Should().NotContain($"b-{start:yyyyMMddHH}");                      // two months old
    }

    [Fact]
    public async Task BackupAsync_CopiesLiveDatabase_OrReturnsNullWhenMissing()
    {
        string root = Path.Combine(Path.GetTempPath(), "gt-backup-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(root, Path.Combine(root, "growth.db"), Path.Combine(root, "backups"),
                                 Path.Combine(root, "logs"), Path.Combine(root, "settings.json"));
        paths.EnsureDirectories();
        var service = new SqliteBackupService(paths, new FakeClock(new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc)));
        try
        {
            (await service.BackupAsync("test")).Should().BeNull();

            using (var connection = new SqliteConnection($"Data Source={paths.DatabasePath};Pooling=False"))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "CREATE TABLE t(x INTEGER); INSERT INTO t VALUES (42);";
                cmd.ExecuteNonQuery();
            }

            string? backup = await service.BackupAsync("test");

            backup.Should().NotBeNull();
            Path.GetFileName(backup).Should().Be("growth-20261006-120000-test.db");
            using var copy = new SqliteConnection($"Data Source={backup};Pooling=False");
            copy.Open();
            using var query = copy.CreateCommand();
            query.CommandText = "SELECT x FROM t";
            Convert.ToInt32(query.ExecuteScalar()).Should().Be(42);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
