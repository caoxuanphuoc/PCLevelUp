using GrowthTracker.Application.Abstractions;
using GrowthTracker.Infrastructure.Backup;
using GrowthTracker.Infrastructure.Jobs;
using GrowthTracker.Infrastructure.Notifications;
using GrowthTracker.Infrastructure.Startup;
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
                options.UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True",
                                  sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IBackupService, SqliteBackupService>();
        services.AddScoped<DatabaseInitializer>();

        // Background jobs and OS integration (TDD §7). On Windows the Desktop layer replaces the notifier with
        // its notify icon, so the reminder shows as a toast from the app's own tray icon.
        services.AddSingleton<IStartupRegistration>(_ => StartupRegistration.ForCurrentProcess());
        if (OperatingSystem.IsLinux())
            services.AddSingleton<INotifier, LinuxDbusNotifier>();
        else
            services.AddSingleton<INotifier, LogOnlyNotifier>();
        services.AddHostedService<JobRunnerService>();
        return services;
    }
}
