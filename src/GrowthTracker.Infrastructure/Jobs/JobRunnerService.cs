using GrowthTracker.Application.Jobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrowthTracker.Infrastructure.Jobs;

/// <summary>
/// Runs <see cref="JobScheduler"/> inside the app process, since a desktop app has no server (TDD §7.1).
/// Ticks right away (start-up catch-up) and then every 30 seconds; a failing tick is logged and the loop goes on.
/// </summary>
public sealed class JobRunnerService(JobScheduler scheduler, ILogger<JobRunnerService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await scheduler.TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
