using GrowthTracker.Application.Abstractions;
using GrowthTracker.Infrastructure.Backup;
using GrowthTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True")
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IBackupService, SqliteBackupService>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
