using System.Globalization;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Settings;

/// <summary>User preferences for running in the background (TDD §7.2) and when the daily jobs last ran.</summary>
public sealed record AppSettingsDto(
    bool CloseToTray,
    bool StartWithOs,
    bool RemindersEnabled,
    int ReminderLeadMinutes,
    DateOnly? LastRolloverDate,
    DateOnly? LastRiskScanDate,
    DateOnly? LastBackupDate = null,
    bool AutoCheckUpdates = true,
    bool IncludePrerelease = false);

internal static class SettingKeys
{
    public const string CloseToTray = "CloseToTray";
    public const string StartWithOs = "StartWithOs";
    public const string RemindersEnabled = "RemindersEnabled";
    public const string ReminderLeadMinutes = "ReminderLeadMinutes";
    public const string LastRolloverDate = "Job.Rollover.LastRunDate";
    public const string LastRiskScanDate = "Job.RiskScan.LastRunDate";
    public const string LastBackupDate = "Job.Backup.LastRunDate";
    public const string AutoCheckUpdates = "Update.AutoCheck";
    public const string IncludePrerelease = "Update.IncludePrerelease";

    public static async Task<Dictionary<string, string>> LoadAsync(IAppDbContext db, CancellationToken ct) =>
        await db.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    public static async Task SetAsync(IAppDbContext db, string key, string value, CancellationToken ct)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
            db.AppSettings.Add(new AppSetting(key, value));
        else
            setting.Value = value;
    }

    public static Task SetDateAsync(IAppDbContext db, string key, DateOnly date, CancellationToken ct) =>
        SetAsync(db, key, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct);

    public static AppSettingsDto ToDto(IReadOnlyDictionary<string, string> s) => new(
        CloseToTray: Bool(s, CloseToTray, defaultValue: true),
        StartWithOs: Bool(s, StartWithOs, defaultValue: false),
        RemindersEnabled: Bool(s, RemindersEnabled, defaultValue: true),
        ReminderLeadMinutes: s.TryGetValue(ReminderLeadMinutes, out var lead) && int.TryParse(lead, out var m) ? m : 10,
        LastRolloverDate: Date(s, LastRolloverDate),
        LastRiskScanDate: Date(s, LastRiskScanDate),
        LastBackupDate: Date(s, LastBackupDate),
        AutoCheckUpdates: Bool(s, AutoCheckUpdates, defaultValue: true),
        IncludePrerelease: Bool(s, IncludePrerelease, defaultValue: false));

    private static bool Bool(IReadOnlyDictionary<string, string> s, string key, bool defaultValue) =>
        s.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : defaultValue;

    private static DateOnly? Date(IReadOnlyDictionary<string, string> s, string key) =>
        s.TryGetValue(key, out var v) && DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;
}

// ── GetAppSettings ──────────────────────────────────────────────────────────

public sealed record GetAppSettingsQuery : IQuery<AppSettingsDto>;

internal sealed class GetAppSettingsHandler(IAppDbContext db) : IQueryHandler<GetAppSettingsQuery, AppSettingsDto>
{
    public async Task<AppSettingsDto> HandleAsync(GetAppSettingsQuery query, CancellationToken ct) =>
        SettingKeys.ToDto(await SettingKeys.LoadAsync(db, ct));
}

// ── UpdateBackgroundSettings ────────────────────────────────────────────────

/// <summary>Changes the background-running and update preferences; null leaves a value as it is.</summary>
public sealed record UpdateBackgroundSettingsCommand(bool? CloseToTray = null, bool? StartWithOs = null, bool? RemindersEnabled = null,
                                                     bool? AutoCheckUpdates = null, bool? IncludePrerelease = null)
    : ICommand<AppSettingsDto>;

internal sealed class UpdateBackgroundSettingsHandler(IAppDbContext db, IStartupRegistration startup)
    : ICommandHandler<UpdateBackgroundSettingsCommand, AppSettingsDto>
{
    public async Task<Result<AppSettingsDto>> HandleAsync(UpdateBackgroundSettingsCommand command, CancellationToken ct)
    {
        if (command.StartWithOs is bool start)
        {
            // Apply the OS registration first so the stored value never claims something that failed.
            if (!startup.TrySet(start, out var error))
                return Result<AppSettingsDto>.Failure("settings.startup", error ?? "Could not change start-up registration.");
            await SettingKeys.SetAsync(db, SettingKeys.StartWithOs, start.ToString(), ct);
        }
        if (command.CloseToTray is bool tray)
            await SettingKeys.SetAsync(db, SettingKeys.CloseToTray, tray.ToString(), ct);
        if (command.RemindersEnabled is bool reminders)
            await SettingKeys.SetAsync(db, SettingKeys.RemindersEnabled, reminders.ToString(), ct);
        if (command.AutoCheckUpdates is bool autoCheck)
            await SettingKeys.SetAsync(db, SettingKeys.AutoCheckUpdates, autoCheck.ToString(), ct);
        if (command.IncludePrerelease is bool prerelease)
            await SettingKeys.SetAsync(db, SettingKeys.IncludePrerelease, prerelease.ToString(), ct);

        await db.SaveChangesAsync(ct);
        return Result<AppSettingsDto>.Success(SettingKeys.ToDto(await SettingKeys.LoadAsync(db, ct)));
    }
}
