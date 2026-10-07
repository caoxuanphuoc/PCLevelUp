using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Goals;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Domain.Goals;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels.Goals;

/// <summary>One Goal card on the Goals list (UI Design §6.1, Stitch "goals" screen).</summary>
public sealed partial class GoalCardViewModel(GoalCardDto dto, Func<GoalCardViewModel, Task> open)
{
    public GoalCardDto Dto => dto;
    public Guid Id => dto.Id;
    public string Name => dto.Name;
    public string AreaName => dto.AreaName;
    public string AreaColorHex => dto.AreaColorHex;
    public IBrush AreaBrush => Display.Brush(dto.AreaColorHex);

    public string StatusLabel => Display.GoalStatus(dto.Status).Label;
    public string StatusColorHex => Display.GoalStatus(dto.Status).ColorHex;
    public MaterialIconKind StatusIcon => Display.GoalStatus(dto.Status).Icon;
    public bool IsAtRisk => dto.Status == GoalStatus.AtRisk;

    public string DateRange => Display.DateRange(dto.StartDate, dto.TargetDate);
    public double Progress => (double)dto.Progress;
    public double Expected => (double)dto.ExpectedProgress;
    public string ProgressText => Display.Percent(dto.Progress);
    public string ExpectedText => Display.Percent(dto.ExpectedProgress);

    /// <summary>"(Chậm 13%)" when behind, "(+3% vượt mốc)" when ahead.</summary>
    public string GapText
    {
        get
        {
            decimal gap = Math.Round(dto.Progress - dto.ExpectedProgress, MidpointRounding.AwayFromZero);
            return gap switch
            {
                < 0 => $"(Chậm {-gap:0}%)",
                > 0 => $"(+{gap:0}% vượt mốc)",
                _ => "(Đúng nhịp)",
            };
        }
    }

    public bool HasScore => dto.Score is not null;
    public string ScoreText => dto.Score is { } s ? Math.Round(s, MidpointRounding.AwayFromZero).ToString("0") : "—";
    public string ScoreLabel => dto.Score is { } s ? Display.ScoreTier(s).Label : "Chưa có dữ liệu";
    public IBrush ScoreBrush => Display.Brush(dto.Score is { } s ? Display.ScoreTier(s).ColorHex : "#94A3B8");

    public string MilestoneText => dto.CurrentMilestone ?? (dto.MilestoneCount == 0 ? "Chưa có milestone" : "Đã hoàn thành mọi milestone");
    public string PlanCountText => $"{dto.ActivePlanCount} Plan đang chạy";

    [RelayCommand]
    private Task Open() => open(this);
}
