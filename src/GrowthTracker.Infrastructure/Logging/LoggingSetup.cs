using Serilog;
using Serilog.Events;

namespace GrowthTracker.Infrastructure.Logging;

public static class LoggingSetup
{
    /// <summary>
    /// Daily rolling file log kept for 14 days. Log only ids: never Note contents or Goal names.
    /// </summary>
    public static ILogger CreateLogger(AppPaths paths) =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(Path.Combine(paths.LogDirectory, "growth-.log"),
                          rollingInterval: RollingInterval.Day,
                          retainedFileCountLimit: 14)
            .CreateLogger();
}
