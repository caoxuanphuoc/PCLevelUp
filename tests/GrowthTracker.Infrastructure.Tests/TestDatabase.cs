using GrowthTracker.Application.Abstractions;
using GrowthTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowthTracker.Infrastructure.Tests;

internal sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}

internal sealed class NoBackup : IBackupService
{
    public Task<string?> BackupAsync(string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}

/// <summary>SQLite in-memory database kept alive by one open connection; migrated like the real app.</summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");

    public FakeClock Clock { get; } = new(new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc));

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        await database._connection.OpenAsync();
        await using var db = database.NewContext();
        await new DatabaseInitializer(db, new NoBackup(), NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
        return database;
    }

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditInterceptor(Clock))
            .Options);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
