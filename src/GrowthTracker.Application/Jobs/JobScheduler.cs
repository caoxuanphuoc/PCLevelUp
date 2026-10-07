using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Settings;
using Microsoft.Extensions.Logging;

namespace GrowthTracker.Application.Jobs;

/// <summary>
/// Decides which in-process jobs are due (TDD §7.1). A host calls <see cref="TickAsync"/> about every 30 seconds.
/// Jobs remember their last run date in AppSettings, so runs missed while the app was closed are caught up on
/// the next start:
/// <list type="bullet">
/// <item>Rollover — on the first tick after start, and daily from 00:05.</item>
/// <item>Risk scan — daily from 06:00 (immediately on start if today's run was missed).</item>
/// <item>Backup — once a day, on the first tick.</item>
/// <item>Weekly review — Sunday 20:00, or the first tick after it.</item>
/// <item>Reminders — each session 10 minutes before its preferred time; never replayed for the past.</item>
/// </list>
/// </summary>
public sealed class JobScheduler(IUseCaseDispatcher useCases, INotifier notifier, IClock clock, ILogger<JobScheduler> logger)
{
    public static readonly TimeOnly RolloverTime = new(0, 5);
    public static readonly TimeOnly RiskScanTime = new(6, 0);

    private bool _startedUp;
    private DateTime? _lastReminderCheckUtc;

    /// <summary>Raised after a job changed data, so open screens can refresh.</summary>
    public event EventHandler? DataChanged;

    public async Task TickAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var today = clock.Today;
        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), clock.TimeZone));
        var settings = await useCases.QueryAsync(new GetAppSettingsQuery(), ct);
        bool changed = false;

        if (!_startedUp || (settings.LastRolloverDate != today && localTime >= RolloverTime))
        {
            var result = await useCases.SendAsync(new RunRolloverCommand(), ct);
            if (result.IsSuccess)
            {
                logger.LogInformation("Rollover: {Missed} session(s) missed, {Created} created", result.Value!.MarkedMissed, result.Value.SessionsCreated);
                changed |= result.Value.MarkedMissed + result.Value.SessionsCreated > 0;
            }
        }

        if (settings.LastRiskScanDate != today && localTime >= RiskScanTime)
        {
            var result = await useCases.SendAsync(new RunRiskScanCommand(), ct);
            if (result.IsSuccess)
            {
                logger.LogInformation("Risk scan: {AtRisk} goal(s) at risk", result.Value);
                changed = true;
            }
        }

        // Daily backup on the first tick of each day (TDD §7.1).
        if (settings.LastBackupDate != today)
        {
            var result = await useCases.SendAsync(new RunDailyBackupCommand(), ct);
            if (result is { IsSuccess: true, Value: not null })
                logger.LogInformation("Daily backup written");
        }

        // Weekly review at Sunday 20:00, or on the first tick after it (FR-40).
        var review = await useCases.SendAsync(new RunWeeklyReviewJobCommand(), ct);
        if (review is { IsSuccess: true, Value: { } week })
        {
            logger.LogInformation("Weekly review generated for week {Week}", week.IsoWeek);
            changed = true;
            await notifier.NotifyAsync($"Weekly Review tuần {week.IsoWeek} đã sẵn sàng",
                "Xem lại tuần qua, các gợi ý điều chỉnh và ghi chú cho tuần tới.", ct);
        }

        // Reminders look only at the window since the previous tick; at start-up that window is empty.
        var from = _lastReminderCheckUtc ?? now;
        _lastReminderCheckUtc = now;
        foreach (var reminder in await useCases.QueryAsync(new GetDueRemindersQuery(from, now), ct))
        {
            logger.LogInformation("Reminder for occurrence {OccurrenceId}", reminder.OccurrenceId);
            await notifier.NotifyAsync($"Sắp đến giờ: {reminder.PlanName}",
                $"Buổi lúc {reminder.Time:HH\\:mm}. Mở Growth Tracker để ghi nhận khi hoàn thành.", ct);
        }

        _startedUp = true;
        if (changed)
            DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
