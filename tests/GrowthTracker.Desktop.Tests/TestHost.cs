using Avalonia.Threading;
using GrowthTracker.Application;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels;
using GrowthTracker.Desktop.Views;
using GrowthTracker.Infrastructure;
using GrowthTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GrowthTracker.Desktop.Tests;

public sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}

/// <summary>The real app wiring (Application + Infrastructure + Desktop) on a migrated SQLite in-memory DB.</summary>
public sealed class TestHost : IAsyncDisposable
{
    public static readonly DateOnly Today = new(2026, 10, 5); // a Monday

    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");

    private TestHost() { }

    public ServiceProvider Services { get; private set; } = null!;
    public FakeClock Clock { get; } = new(Today.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc));
    public IUseCaseDispatcher UseCases => Services.GetRequiredService<IUseCaseDispatcher>();
    public IDialogService Dialogs => Services.GetRequiredService<IDialogService>();
    public INavigationService Navigation => Services.GetRequiredService<INavigationService>();
    public MainWindow? Window { get; private set; }
    public FakeStartupRegistration Startup { get; } = new();
    public FakeUpdateService Updates { get; } = new();

    public static async Task<TestHost> CreateAsync()
    {
        var host = new TestHost();
        await host._connection.OpenAsync();
        var services = new ServiceCollection()
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Warning))
            .AddApplication()
            .AddInfrastructure(AppPaths.Resolve(false, _ => null, "/tmp"), o => o.UseSqlite(host._connection))
            .AddDesktop();
        services.AddSingleton<IClock>(host.Clock);
        services.AddSingleton<IStartupRegistration>(host.Startup);
        services.AddSingleton<IUpdateService>(host.Updates);
        host.Services = services.BuildServiceProvider();

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        return host;
    }

    public (MainWindow Window, MainWindowViewModel Vm) OpenMainWindow()
    {
        var vm = Services.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = vm };
        Window = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    /// <summary>Pumps the UI dispatcher until a condition holds (async loads, dialogs opening).</summary>
    public static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var started = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - started > timeoutMs)
                throw new TimeoutException("Condition was not met in time.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Logs the plan's session on <paramref name="date"/>, with the clock set to that evening.</summary>
    public async Task LogSessionAsync(Guid planId, DateOnly date, Application.Logging.LogEntry entry)
    {
        Clock.UtcNow = date.ToDateTime(new TimeOnly(19, 0), DateTimeKind.Utc);
        Guid id;
        await using (var scope = Services.CreateAsyncScope())
            id = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PlanOccurrences
                .Where(o => o.PlanId == planId && o.ScheduledDate == date).Select(o => o.Id).SingleAsync();
        var result = await UseCases.SendAsync(new Application.Logging.LogPlanResultCommand(id, entry));
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error!.Message);
    }

    public async Task<TDialog> WaitForDialogAsync<TDialog>() where TDialog : class
    {
        await WaitForAsync(() => Dialogs.Current is TDialog);
        return (TDialog)(object)Dialogs.Current!;
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

/// <summary>Never touches the real registry or autostart folder.</summary>
public sealed class FakeStartupRegistration : IStartupRegistration
{
    public bool IsRegistered { get; private set; }

    public bool TrySet(bool enabled, out string? error)
    {
        IsRegistered = enabled;
        error = null;
        return true;
    }
}

/// <summary>Stands in for Velopack: scripted check results, no network, no restart.</summary>
public sealed class FakeUpdateService : IUpdateService
{
    public bool IsSupported { get; set; } = true;
    public string CurrentVersion { get; set; } = "1.0.0";
    public string? Available { get; set; }
    public Exception? CheckError { get; set; }
    public bool? LastIncludePrerelease { get; private set; }
    public int Downloads { get; private set; }
    public bool AppliedOnExit { get; private set; }

    public Task<string?> CheckAsync(bool includePrerelease, CancellationToken cancellationToken = default)
    {
        LastIncludePrerelease = includePrerelease;
        return CheckError is not null ? Task.FromException<string?>(CheckError) : Task.FromResult(Available);
    }

    public Task DownloadAsync(Action<int>? progress = null, CancellationToken cancellationToken = default)
    {
        Downloads++;
        progress?.Invoke(50);
        return Task.CompletedTask;
    }

    public void ApplyOnExitAndRestart() => AppliedOnExit = true;
}
