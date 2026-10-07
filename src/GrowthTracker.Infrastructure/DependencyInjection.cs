using GrowthTracker.Application.Abstractions;
using GrowthTracker.Infrastructure.Backup;
using GrowthTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Infrastructure;

public static class DependencyInjection
{
    /// <param name="configureDatabase">Overrides the SQLite file connection (tests pass an in-memory connection).</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, AppPaths paths,
                                                       Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.AddSingleton(paths);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            if (configureDatabase is not null)
                configureDatabase(options);
            else
                options.UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True");
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IBackupService, SqliteBackupService>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
