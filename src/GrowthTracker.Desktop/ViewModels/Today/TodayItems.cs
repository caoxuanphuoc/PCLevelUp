using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Logging;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Domain.Gamification;
using GrowthTracker.Domain.Plans;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels.Today;

public sealed record SkipOption(SkipReason Reason, string Label);

/// <summary>Actions the Today screen performs for a session row.</summary>
public interface ISessionActions
{
    Task OpenAsync(SessionItemViewModel session);
    Task QuickDoneAsync(SessionItemViewModel session);
    Task SkipAsync(SessionItemViewModel session, SkipReason reason);
}

/// <summary>One session on Today: pending (log / tick / skip), logged (score, stars, edit window) or skipped.</summary>
public sealed partial class SessionItemViewModel(TodaySessionDto dto, DateTime nowUtc, TimeZoneInfo timeZone, ISessionActions actions)
{
    public static IReadOnlyList<SkipOption> SkipOptions { get; } =
    [
        new(SkipReason.Busy, "Bận"),
        new(SkipReason.Sick, "Ốm"),
        new(SkipReason.NotInTheMood, "Không muốn"),
        new(SkipReason.Other, "Khác"),
    ];

    public TodaySessionDto Dto => dto;
    public Guid OccurrenceId => dto.OccurrenceId;
    public string PlanName => dto.PlanName;
    public string AreaName => dto.AreaName;
    public string AreaColorHex => dto.AreaColorHex;
    public string TypeLabel => Display.PlanType(dto.PlanType).Label;
    public MaterialIconKind TypeIcon => Display.PlanType(dto.PlanType).Icon;

    public bool IsLogged => dto.Log is not null;
    public bool IsDone => dto.State == OccurrenceState.Done;
    public bool IsSkipped => dto.State == OccurrenceState.Skipped;
    public bool IsPending => !IsLogged && !IsSkipped;
    /// <summary>Binary plans are ticked with one click (UI Design §5.2).</summary>
    public bool CanQuickTick => IsPending && dto.PlanType == PlanType.Binary;

    public string TargetText => "Mục tiêu: " + Display.Target(dto.PlanType, dto.TargetValue, dto.TargetUnit, dto.FrequencyPerWeek, dto.CriteriaTotal);
    public string TimeText => dto.ScheduledTime?.ToString("HH:mm") ?? "";
    public bool HasTime => dto.ScheduledTime is not null;
    public string? GoalName => dto.GoalName;

    /// <summary>"25/30 phút", "Đã thực hiện", "1/3 tiêu chí".</summary>
    public string ResultText => dto.Log is not { } log ? ""
        : dto.PlanType switch
        {
            PlanType.Time or PlanType.Quantity => $"{Display.Number(log.ActualValue)}/{Display.Number(dto.TargetValue)} {dto.TargetUnit ?? "phút"}",
            PlanType.Achievement => $"{dto.CriteriaDone}/{dto.CriteriaTotal} tiêu chí",
            _ => IsDone ? "Đã thực hiện" : "Không thực hiện",
        };

    public int Quality => dto.Log?.Quality ?? 0;
    public string ScoreText => dto.Log?.Score.ToString() ?? "";
    public string ScoreLabel => dto.Log is { } log ? Display.ScoreTier(log.Score).Label : "";
    public string ScoreColorHex => dto.Log is { } log ? Display.ScoreTier(log.Score).ColorHex : "#94A3B8";

    public string SavedText
    {
        get
        {
            if (dto.Log is not { } log)
                return "";
            var saved = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(log.LoggedAt, DateTimeKind.Utc), timeZone);
            if (!log.CanEdit)
                return $"Đã lưu {saved:HH:mm}";
            var left = log.EditableUntil - nowUtc;
            return $"Đã lưu {saved:HH:mm} · Nhấn để sửa (còn {Math.Max(1, (int)Math.Ceiling(left.TotalHours))}h)";
        }
    }

    public string SkipText => dto.SkipReason is { } reason ? "Lý do: " + SkipOptions.First(o => o.Reason == reason).Label : "";

    [RelayCommand]
    private Task Open() => actions.OpenAsync(this);

    [RelayCommand]
    private Task QuickDone() => actions.QuickDoneAsync(this);

    [RelayCommand]
    private Task Skip(SkipReason reason) => actions.SkipAsync(this, reason);
}

/// <summary>A section of Today: logged, morning/afternoon/evening/any time, or skipped.</summary>
public sealed partial class SessionGroupViewModel(string title, MaterialIconKind icon, string? note, IReadOnlyList<SessionItemViewModel> items,
                                                  bool collapsible = false) : ObservableObject
{
    public string Title => $"{title} ({items.Count})";
    public MaterialIconKind Icon => icon;
    public string? Note => note;
    public bool HasNote => !string.IsNullOrEmpty(note);
    public IReadOnlyList<SessionItemViewModel> Items => items;
    public bool IsCollapsible => collapsible;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = !collapsible;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>A day in the week rhythm strip.</summary>
public sealed record WeekDayItem(string Label, string ScoreText, bool IsToday, bool HasScore, IBrush Fill, IBrush Foreground);

public sealed record AreaLevelItem(string Name, string ColorHex, string LevelText, string XpText, double Progress)
{
    public static AreaLevelItem From(AreaLevelDto dto)
    {
        var level = dto.Level;
        double progress = level.NextLevelXp is { } next ? (double)((level.TotalXp - level.LevelStartXp) / (next - level.LevelStartXp) * 100) : 100;
        return new AreaLevelItem(dto.Name, dto.ColorHex, $"Level {level.Level}", $"{Display.Number(level.TotalXp)} XP", progress);
    }

    public static string Describe(LevelInfo level) => $"Level {level.Level} • {Display.Number(level.TotalXp)} XP";
}
