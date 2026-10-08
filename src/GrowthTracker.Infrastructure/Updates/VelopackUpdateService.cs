using System.Reflection;
using GrowthTracker.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace GrowthTracker.Infrastructure.Updates;

/// <summary>
/// Where releases are published (TDD §8.4): GitHub Releases of the app's repository, over HTTPS.
/// <c>GROWTHTRACKER_UPDATE_FEED</c> (a local folder or URL produced by <c>vpk pack</c>) overrides it to test updates.
/// </summary>
public sealed record UpdateFeed(string RepositoryUrl, string? LocalFeed = null)
{
    public const string OverrideVariable = "GROWTHTRACKER_UPDATE_FEED";

    public static UpdateFeed Default { get; } = new("https://github.com/caoxuanphuoc/PCLevelUp");

    public static UpdateFeed FromEnvironment(Func<string, string?> env) =>
        env(OverrideVariable) is { Length: > 0 } local ? Default with { LocalFeed = local } : Default;

    internal IUpdateSource CreateSource(bool includePrerelease) => LocalFeed is null
        ? new GithubSource(RepositoryUrl, accessToken: null, prerelease: includePrerelease)
        : LocalFeed.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new SimpleWebSource(LocalFeed) : new SimpleFileSource(new DirectoryInfo(LocalFeed));
}

/// <summary>
/// Velopack updater (TDD §8.3). The channel (win, win-arm64, linux) comes from the installed package, so each
/// platform only sees its own builds. Stable and beta are split by GitHub's pre-release flag.
/// </summary>
public sealed class VelopackUpdateService(UpdateFeed feed, ILogger<VelopackUpdateService> logger) : IUpdateService
{
    private readonly Lazy<UpdateManager?> _installed = new(() => TryCreate(feed, includePrerelease: false, logger));
    private UpdateManager? _manager;
    private UpdateInfo? _pending;

    public bool IsSupported => _installed.Value is { IsInstalled: true };

    public string CurrentVersion => _installed.Value?.CurrentVersion?.ToString() ?? AssemblyVersion();

    public async Task<string?> CheckAsync(bool includePrerelease, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return null;
        _manager = TryCreate(feed, includePrerelease, logger) ?? _installed.Value!;
        _pending = await _manager.CheckForUpdatesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Update check: current {Current}, available {Available}", CurrentVersion,
                              _pending?.TargetFullRelease.Version.ToString() ?? "none");
        return _pending?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadAsync(Action<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_manager is null || _pending is null)
            throw new InvalidOperationException("No update to download; call CheckAsync first.");
        await _manager.DownloadUpdatesAsync(_pending, progress, cancellationToken);
    }

    public void ApplyOnExitAndRestart()
    {
        if (_manager is null || _pending is null)
            throw new InvalidOperationException("No downloaded update.");
        _manager.WaitExitThenApplyUpdates(_pending.TargetFullRelease, silent: false, restart: true);
    }

    private static UpdateManager? TryCreate(UpdateFeed feed, bool includePrerelease, ILogger logger)
    {
        try
        {
            return new UpdateManager(feed.CreateSource(includePrerelease));
        }
        catch (Exception ex)
        {
            // Not installed by Velopack (dev run, tests): updates are simply unavailable.
            logger.LogDebug(ex, "Velopack update manager unavailable");
            return null;
        }
    }

    private static string AssemblyVersion()
    {
        string? version = (Assembly.GetEntryAssembly() ?? typeof(VelopackUpdateService).Assembly)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return version?.Split('+')[0] ?? "0.0.0";
    }
}
