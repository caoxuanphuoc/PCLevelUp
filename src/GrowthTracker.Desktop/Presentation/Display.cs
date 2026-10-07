using System.Globalization;
using Avalonia.Media;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;
using Material.Icons;

namespace GrowthTracker.Desktop.Presentation;

public sealed record StatusLook(string Label, string ColorHex, MaterialIconKind Icon);

/// <summary>
/// "One color, one meaning" (UI Design §1): fixed label, color and icon per status, score tier and plan type.
/// Hues match Styles/Tokens.axaml and stay the same in dark mode.
/// </summary>
public static class Display
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("vi-VN");

    public static StatusLook GoalStatus(GoalStatus status) => status switch
    {
        Domain.Goals.GoalStatus.Draft => new("Draft", "#6B7280", MaterialIconKind.CircleOutline),
        Domain.Goals.GoalStatus.Active => new("Active", "#2563EB", MaterialIconKind.Circle),
        Domain.Goals.GoalStatus.OnTrack => new("On Track", "#16A34A", MaterialIconKind.CheckCircleOutline),
        Domain.Goals.GoalStatus.AtRisk => new("At Risk", "#D97706", MaterialIconKind.AlertOutline),
        Domain.Goals.GoalStatus.Completed => new("Completed", "#15803D", MaterialIconKind.Star),
        _ => new("Cancelled", "#9CA3AF", MaterialIconKind.Close),
    };

    public static StatusLook PlanStatus(PlanStatus status) => status switch
    {
        Domain.Plans.PlanStatus.Active => new("Đang chạy", "#16A34A", MaterialIconKind.Check),
        Domain.Plans.PlanStatus.Paused => new("Tạm dừng", "#6B7280", MaterialIconKind.Pause),
        _ => new("Đã lưu trữ", "#9CA3AF", MaterialIconKind.ArchiveOutline),
    };

    /// <summary>Score tiers from UI Design §2.3.</summary>
    public static (string Label, string ColorHex) ScoreTier(decimal score) => score switch
    {
        >= 90 => ("Xuất sắc", "#15803D"),
        >= 75 => ("Tốt", "#16A34A"),
        >= 60 => ("Đạt", "#CA8A04"),
        >= 40 => ("Cần cải thiện", "#EA580C"),
        _ => ("Yếu", "#DC2626"),
    };

    public static (string Label, MaterialIconKind Icon) PlanType(PlanType type) => type switch
    {
        Domain.Plans.PlanType.Time => ("Thời gian", MaterialIconKind.TimerOutline),
        Domain.Plans.PlanType.Quantity => ("Số lượng", MaterialIconKind.Counter),
        Domain.Plans.PlanType.Frequency => ("Tần suất", MaterialIconKind.RepeatVariant),
        Domain.Plans.PlanType.Binary => ("Có / Không", MaterialIconKind.ToggleSwitchOutline),
        _ => ("Thành tựu", MaterialIconKind.TrophyOutline),
    };

    public static string Difficulty(Difficulty difficulty) => difficulty switch
    {
        Domain.Plans.Difficulty.Easy => "Dễ",
        Domain.Plans.Difficulty.Normal => "Bình thường",
        Domain.Plans.Difficulty.Hard => "Khó",
        _ => "Rất khó",
    };

    /// <summary>Goal priority 1 (highest) … 4 (lowest).</summary>
    public static (string Label, string ColorHex) Priority(int priority) => priority switch
    {
        1 => ("Rất cao", "#DC2626"),
        2 => ("Cao", "#EA580C"),
        3 => ("Trung bình", "#CA8A04"),
        _ => ("Thấp", "#6B7280"),
    };

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string DateRange(DateOnly start, DateOnly end) => $"{Date(start)} → {Date(end)}";

    public static string Percent(decimal value) => $"{Math.Round(value, MidpointRounding.AwayFromZero):0}%";

    public static string Number(decimal value) => value.ToString("0.##", Culture);

    private static readonly (WeekDays Day, string Label)[] DayLabels =
    [
        (WeekDays.Monday, "T2"), (WeekDays.Tuesday, "T3"), (WeekDays.Wednesday, "T4"), (WeekDays.Thursday, "T5"),
        (WeekDays.Friday, "T6"), (WeekDays.Saturday, "T7"), (WeekDays.Sunday, "CN"),
    ];

    public static IReadOnlyList<(WeekDays Day, string Label)> WeekDayLabels => DayLabels;

    public static string Days(WeekDays days) => days switch
    {
        WeekDays.None => "Chưa có lịch",
        WeekDays.All => "Hằng ngày",
        WeekDays.Weekdays => "T2 – T6",
        _ => string.Join(", ", DayLabels.Where(d => (days & d.Day) != 0).Select(d => d.Label)),
    };

    /// <summary>"20 phút/buổi", "3 buổi/tuần", "Mỗi ngày: có/không", "2 tiêu chí".</summary>
    public static string Target(PlanType type, decimal target, string? unit, int? perWeek, int criteriaTotal) => type switch
    {
        Domain.Plans.PlanType.Time => $"{Number(target)} {unit ?? "phút"}/buổi",
        Domain.Plans.PlanType.Quantity => $"{Number(target)} {unit ?? "lần"}/buổi",
        Domain.Plans.PlanType.Frequency => $"{perWeek ?? (int)target} buổi/tuần",
        Domain.Plans.PlanType.Binary => "Có / Không mỗi buổi",
        _ => $"{criteriaTotal} tiêu chí",
    };

    public static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    /// <summary>The ~12% tint used behind chips and banners (Stitch design: "10% opacity tint").</summary>
    public static IBrush Tint(string hex, byte alpha = 0x1F)
    {
        var c = Color.Parse(hex);
        return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
    }
}
