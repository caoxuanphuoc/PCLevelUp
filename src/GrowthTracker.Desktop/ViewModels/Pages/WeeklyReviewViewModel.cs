using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Reviews;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Reviews;
using Material.Icons;

namespace GrowthTracker.Desktop.ViewModels.Pages;

public sealed partial class AreaReviewRow(AreaReviewDto dto, Func<AreaReviewRow, Task> open)
{
    public string Name => dto.Name;
    public IBrush ColorBrush => Display.Brush(dto.ColorHex);
    public string ScoreText => dto.Score is { } s ? Math.Round(s, MidpointRounding.AwayFromZero).ToString("0") : "—";
    public string PreviousText => dto.PreviousScore is { } p ? Math.Round(p, MidpointRounding.AwayFromZero).ToString("0") : "—";
    public IBrush ScoreBrush => Display.Brush(dto.Status == AreaReviewStatus.NeedsAttention ? "#DC2626" : "#0F172A");
    public MaterialIconKind TrendIcon => Trends.Icon(dto.Trend);
    public IBrush TrendBrush => Trends.Brush(dto.Trend);
    public string TrendText => dto.Score is { } s && dto.PreviousScore is { } p
        ? $"{(s - p >= 0 ? "+" : "")}{Display.Number(Math.Round(s - p, MidpointRounding.AwayFromZero))} điểm"
        : "—";
    public string StatusText => dto.Status switch
    {
        AreaReviewStatus.Excellent => "Xuất sắc",
        AreaReviewStatus.NeedsAttention => "Cần chú ý",
        _ => "On Track",
    };
    public string StatusColorHex => dto.Status switch
    {
        AreaReviewStatus.Excellent => "#15803D",
        AreaReviewStatus.NeedsAttention => "#DC2626",
        _ => "#2563EB",
    };
    public MaterialIconKind StatusIcon => dto.Status switch
    {
        AreaReviewStatus.Excellent => MaterialIconKind.Star,
        AreaReviewStatus.NeedsAttention => MaterialIconKind.AlertOutline,
        _ => MaterialIconKind.Check,
    };
    public bool NeedsAttention => dto.Status == AreaReviewStatus.NeedsAttention;
    public Guid AreaId => dto.AreaId;

    [RelayCommand]
    private Task Open() => open(this);
}

/// <summary>A target suggestion (FR-52, FR-53): "Áp dụng" changes the plan after confirmation, "Bỏ qua" hides it 4 weeks.</summary>
public sealed partial class SuggestionItem(SuggestionDto dto, Func<SuggestionItem, Task> apply, Func<SuggestionItem, Task> dismiss)
{
    public SuggestionDto Dto => dto;
    public string PlanName => dto.PlanName;
    public bool IsDecrease => dto.Kind == RecommendationKind.DecreaseTarget;
    public MaterialIconKind Icon => IsDecrease ? MaterialIconKind.ArrowCollapseDown : MaterialIconKind.ArrowExpandUp;
    public string IconColorHex => IsDecrease ? "#EA580C" : "#16A34A";
    public string Unit => dto.PlanType == PlanType.Frequency ? "buổi/tuần" : dto.Unit ?? (dto.PlanType == PlanType.Time ? "phút" : "lần");

    /// <summary>UI Design §10.5: "Giảm xuống 3 buổi/tuần có thể phù hợp hơn."</summary>
    public string Text => IsDecrease
        ? $"Giảm từ {Display.Number(dto.CurrentValue)} xuống {Display.Number(dto.SuggestedValue)} {Unit} có thể phù hợp hơn."
        : $"Tăng từ {Display.Number(dto.CurrentValue)} lên {Display.Number(dto.SuggestedValue)} {Unit}: bạn đang làm rất đều.";
    public string Reason => dto.Reason;
    public bool IsPending => dto.State == RecommendationState.Pending;
    public string StateText => dto.State switch
    {
        RecommendationState.Applied => "Đã áp dụng",
        RecommendationState.Dismissed => "Đã bỏ qua 4 tuần",
        _ => "",
    };

    [RelayCommand]
    private Task Apply() => apply(this);

    [RelayCommand]
    private Task Dismiss() => dismiss(this);
}

/// <summary>
/// Weekly Review (FR-40 → FR-43, FR-52 → FR-54, UI Design §8): week summary, Areas this week vs last week,
/// highlights and attention points, target suggestions, a note, and "Hoàn tất review".
/// </summary>
public sealed partial class WeeklyReviewViewModel(IUseCaseDispatcher dispatcher, IDialogService dialogs, INavigationService navigation)
    : PageViewModel
{
    public override string Title => "Đánh giá tuần (Weekly Review)";

    public ObservableCollection<AreaReviewRow> Areas { get; } = [];
    public ObservableCollection<ReviewNoteDto> Highlights { get; } = [];
    public ObservableCollection<ReviewNoteDto> Attention { get; } = [];
    public ObservableCollection<SuggestionItem> Suggestions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekText), nameof(RangeText), nameof(StatusText), nameof(StatusColorHex), nameof(ScoreValue),
                              nameof(ScoreText), nameof(ScoreLabel), nameof(ScoreBrush), nameof(ScoreDeltaText), nameof(ScoreDeltaBrush),
                              nameof(DoneText), nameof(DoneProgress), nameof(ConsistencyText), nameof(ConsistencyDeltaText), nameof(XpText),
                              nameof(XpDeltaText), nameof(StreakText), nameof(ActiveDaysText), nameof(HasHighlights), nameof(HasAttention),
                              nameof(SuggestionHint), nameof(ShowSuggestionHint), nameof(CanGoNext), nameof(IsCompleted), nameof(CompleteText))]
    public partial WeeklyReviewDto? Data { get; private set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial string? ActionError { get; private set; }

    [ObservableProperty]
    public partial string? SavedText { get; private set; }

    private DateOnly? _weekStart;

    // ── Header ──
    public string WeekText => Data is null ? "" : $"Tuần {Data.IsoWeek}";
    public string RangeText => Data is null ? "" : $"Khoảng thời gian: {Display.DateRange(Data.WeekStart, Data.WeekEnd)}";
    public string StatusText => Data switch
    {
        null => "",
        { IsCompleted: true } => "Đã hoàn thành",
        { IsProvisional: true } => "Tạm tính",
        _ => "Chưa review",
    };
    public string StatusColorHex => Data switch
    {
        { IsCompleted: true } => "#15803D",
        { IsProvisional: true } => "#CA8A04",
        _ => "#2563EB",
    };
    public bool CanGoNext => Data?.CanGoNext == true;
    public bool IsCompleted => Data?.IsCompleted == true;
    public string CompleteText => Data is null ? "" : IsCompleted ? $"Đã hoàn tất review tuần {Data.IsoWeek}" : $"Hoàn tất review tuần {Data.IsoWeek}";

    // ── Summary ──
    public double ScoreValue => (double)(Data?.Score ?? 0);
    public string ScoreText => Data?.Score is { } s ? Math.Round(s, MidpointRounding.AwayFromZero).ToString("0") : "—";
    public string ScoreLabel => Data?.Score is { } s ? Display.ScoreTier(s).Label.ToUpperInvariant() : "CHƯA CÓ DỮ LIỆU";
    public IBrush ScoreBrush => Display.Brush(Data?.Score is { } s ? Display.ScoreTier(s).ColorHex : "#94A3B8");
    public string ScoreDeltaText => Data is { Score: { } s, PreviousScore: { } p }
        ? $"{(s - p >= 0 ? "↑ +" : "↓ ")}{Display.Number(Math.Round(s - p, 1))} điểm so với tuần trước"
        : "Chưa có tuần trước để so sánh";
    public IBrush ScoreDeltaBrush => Trends.Brush(Data?.Trend);
    public string DoneText => Data is null ? "" : $"{Data.PlansDone} / {Data.PlansTotal} buổi";
    public double DoneProgress => Data is { PlansTotal: > 0 } d ? d.PlansDone * 100.0 / d.PlansTotal : 0;
    public string ConsistencyText => Data is null ? "" : Display.Percent(Data.ConsistencyPct);
    public string ConsistencyDeltaText => Data?.PreviousConsistencyPct is { } p
        ? $"{(Data.ConsistencyPct - p >= 0 ? "+" : "")}{Display.Number(Math.Round(Data.ConsistencyPct - p, 1))}% so với tuần trước"
        : "";
    public string XpText => Data is null ? "" : $"+{Display.Number(Data.XpGained)}";
    public string XpDeltaText => Data is null ? "" : $"Tuần trước: +{Display.Number(Data.PreviousXpGained)} XP";
    public string StreakText => Data is null ? "" : $"{Data.Streak}";
    public string ActiveDaysText => Data is null ? "" : $"{Data.ActiveDays}/7 ngày hoạt động";

    public bool HasHighlights => Highlights.Count > 0;
    public bool HasAttention => Attention.Count > 0;

    /// <summary>Why no suggestion is shown (UI Design §8: fewer than 3 logged days, or the report is not created yet).</summary>
    public string SuggestionHint => Data switch
    {
        null => "",
        { CanSuggest: false } => "Cần thêm dữ liệu để đưa ra gợi ý (ít nhất 3 ngày có ghi nhận trong tuần).",
        { IsGenerated: false } => "Gợi ý xuất hiện khi báo cáo tuần được tạo (Chủ nhật 20:00).",
        _ => "Không có điều chỉnh nào cần thiết: các kế hoạch đang vừa sức.",
    };
    public bool ShowSuggestionHint => Data is not null && Suggestions.Count == 0;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is DateOnly week)
            _weekStart = week;
        return ReloadAsync();
    }

    public Task ReloadAsync() => LoadSafelyAsync(async () =>
    {
        var data = await dispatcher.QueryAsync(new GetWeeklyReviewQuery(_weekStart));
        _weekStart = data.WeekStart;

        Areas.Clear();
        foreach (var area in data.Areas)
            Areas.Add(new AreaReviewRow(area, row => navigation.NavigateAsync<GoalsViewModel>(row.AreaId)));
        Highlights.Clear();
        foreach (var h in data.Highlights)
            Highlights.Add(h);
        Attention.Clear();
        foreach (var a in data.Attention)
            Attention.Add(a);
        Suggestions.Clear();
        foreach (var s in data.Suggestions)
            Suggestions.Add(new SuggestionItem(s, ApplyAsync, DismissAsync));

        Note = data.Note;
        SavedText = null;
        Data = data;
    });

    [RelayCommand]
    private Task PreviousWeek() => ShiftWeek(-7);

    [RelayCommand]
    private Task NextWeek() => CanGoNext ? ShiftWeek(7) : Task.CompletedTask;

    private Task ShiftWeek(int days)
    {
        if (_weekStart is { } week)
            _weekStart = week.AddDays(days);
        return ReloadAsync();
    }

    [RelayCommand]
    private async Task SaveNote()
    {
        if (Data is null)
            return;
        var result = await dispatcher.SendAsync(new SaveWeeklyReviewNoteCommand(Data.WeekStart, Note));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        SavedText = result.IsSuccess ? "Đã lưu ghi chú" : null;
    }

    /// <summary>"Hoàn tất review": marks the week reviewed and hides the Dashboard reminder.</summary>
    [RelayCommand]
    private async Task Complete()
    {
        if (Data is null)
            return;
        var result = await dispatcher.SendAsync(new CompleteWeeklyReviewCommand(Data.WeekStart, Note));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        await ReloadAsync();
    }

    private async Task ApplyAsync(SuggestionItem item)
    {
        bool confirmed = await dialogs.ConfirmAsync(
            "Áp dụng gợi ý?",
            $"{item.PlanName}: {item.Text} Thay đổi chỉ áp dụng cho các buổi từ ngày mai; lịch sử và Score đã có giữ nguyên.",
            "Áp dụng");
        if (!confirmed)
            return;
        var result = await dispatcher.SendAsync(new ApplyRecommendationCommand(item.Dto.Id));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        await ReloadAsync();
    }

    private async Task DismissAsync(SuggestionItem item)
    {
        var result = await dispatcher.SendAsync(new DismissRecommendationCommand(item.Dto.Id));
        ActionError = result.IsSuccess ? null : ErrorMessages.For(result.Error!);
        await ReloadAsync();
    }
}
