using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Areas;
using GrowthTracker.Application.Goals;
using GrowthTracker.Domain.Common;
using GrowthTracker.Infrastructure;
using GrowthTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GrowthTracker.Application.Tests;

public sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}

public sealed class FakeNotifier : INotifier
{
    public List<(string Title, string Message)> Sent { get; } = [];

    public Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Sent.Add((title, message));
        return Task.CompletedTask;
    }
}

/// <summary>Never touches the real registry or autostart folder.</summary>
public sealed class FakeStartupRegistration : IStartupRegistration
{
    public bool IsRegistered { get; private set; }
    public bool Fail { get; set; }

    public bool TrySet(bool enabled, out string? error)
    {
        if (Fail)
        {
            error = "denied";
            return false;
        }
        IsRegistered = enabled;
        error = null;
        return true;
    }
}

/// <summary>Full Application + Infrastructure wiring on a migrated SQLite in-memory database.</summary>
public sealed class TestApp : IAsyncDisposable
{
    public static readonly DateOnly Today = new(2026, 10, 5); // a Monday

    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private ServiceProvider _services = null!;

    public FakeClock Clock { get; } = new(Today.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc));
    public IUseCaseDispatcher Dispatcher => _services.GetRequiredService<IUseCaseDispatcher>();
    public FakeNotifier Notifier { get; } = new();
    public FakeStartupRegistration Startup { get; } = new();
    public T Get<T>() where T : notnull => _services.GetRequiredService<T>();

    public static async Task<TestApp> CreateAsync()
    {
        var app = new TestApp();
        await app._connection.OpenAsync();
        var services = new ServiceCollection()
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Warning))
            .AddApplication()
            .AddInfrastructure(AppPaths.Resolve(false, _ => null, "/tmp"), o => o.UseSqlite(app._connection));
        services.AddSingleton<IClock>(app.Clock);
        services.AddSingleton<INotifier>(app.Notifier);
        services.AddSingleton<IStartupRegistration>(app.Startup);
        app._services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        await using var scope = app._services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        return app;
    }

    /// <summary>Direct DB access for assertions and setup the use cases don't cover.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    public async Task<Guid> AreaIdAsync(string name) =>
        (await Dispatcher.QueryAsync(new GetAreasQuery())).Single(a => a.Name == name).Id;

    public async Task<Guid> CreateGoalAsync(string name = "Improve English", string area = "English", int priority = 2,
                                            DateOnly? start = null, DateOnly? target = null)
    {
        var fields = new GoalFields(await AreaIdAsync(area), name, start ?? Today, target ?? Today.AddMonths(6), priority);
        return (await Dispatcher.SendAsync(new CreateGoalCommand(fields))).Value;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal static class ResultAssertions
{
    public static T ShouldSucceed<T>(this Result<T> result)
    {
        result.Error.Should().BeNull();
        return result.Value!;
    }

    public static void ShouldFailWith<T>(this Result<T> result, string code)
    {
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(code);
    }
}
