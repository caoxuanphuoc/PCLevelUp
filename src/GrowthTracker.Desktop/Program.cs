using Avalonia;
using Avalonia.Media;
using GrowthTracker.Application;
using GrowthTracker.Infrastructure;
using GrowthTracker.Infrastructure.Logging;
using GrowthTracker.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Velopack;

namespace GrowthTracker.Desktop;

internal static class Program
{
    // Don't use Avalonia or any SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static int Main(string[] args)
    {
        // Must run first: Velopack may handle install/update hooks and exit the process.
        VelopackApp.Build().Run();

        var paths = AppPaths.ForCurrentOs();
        paths.EnsureDirectories();
        Log.Logger = LoggingSetup.CreateLogger(paths);
        GlobalExceptionHandlers.RegisterProcessWide();

        try
        {
            using var host = AppHost.Create(paths);
            using (var scope = host.Services.CreateScope())
                scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync().GetAwaiter().GetResult();

            host.Start(); // background jobs (rollover, reminders, backup) hook in here from Sprint 4
            App.Services = host.Services;
            int exitCode = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            host.StopAsync().GetAwaiter().GetResult();
            return exitCode;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
            .LogToTrace();
}

internal static class AppHost
{
    public static IHost Create(AppPaths paths)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSerilog();
        builder.Services
            .AddApplication()
            .AddInfrastructure(paths)
            .AddDesktop();
        return builder.Build();
    }
}
