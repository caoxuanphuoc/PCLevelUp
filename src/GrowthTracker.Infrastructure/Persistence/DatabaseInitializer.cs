using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Areas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GrowthTracker.Infrastructure.Persistence;

/// <summary>Startup sequence: back up → migrate → enable WAL → seed default Areas on first run.</summary>
public sealed class DatabaseInitializer(AppDbContext db, IBackupService backup, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            string? backupPath = await backup.BackupAsync("pre-migrate", cancellationToken);
            logger.LogInformation("Applying {Count} migration(s); backup: {BackupPath}", pending.Count, backupPath ?? "none (new database)");
            await db.Database.MigrateAsync(cancellationToken);
        }

        // WAL is persisted in the database file; foreign keys are enabled per connection by the connection string.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        await SeedAsync(cancellationToken);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Areas.IgnoreQueryFilters().AnyAsync(cancellationToken))
            return;

        for (int i = 0; i < Area.Defaults.Count; i++)
            db.Areas.Add(new Area(Area.Defaults[i].Name, Area.Defaults[i].ColorHex, i));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} default areas", Area.Defaults.Count);
    }
}
