using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Logging;
using GrowthTracker.Desktop.Presentation;
using GrowthTracker.Domain.Plans;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

public sealed partial class CriterionItem(Guid id, string title, bool isDone) : ObservableObject
{
    public Guid Id => id;
    public string Title => title;

    [ObservableProperty]
    public partial bool IsDone { get; set; } = isDone;
}

/// <summary>One of the four Score components in the result breakdown.</summary>
public sealed record BreakdownItem(string Title, string ValueText, string Detail, double Percent, IBrush Brush);

/// <summary>
/// Plan Result (UI Design §8, Stitch "plan result"): step 1 records Actual, Quality and Note — the input changes
/// with the plan type, Actual defaults to Target, and there is no Score field (BR-03). Step 2 shows the computed
/// Score, its breakdown, the XP earned and the Area level. Returns the saved result, or null if nothing was saved.
/// </summary>
public sealed partial class PlanResultViewModel : DialogViewModel<LogResultDto?>
{
    private static readonly string[] QualityLabels = ["", "Kém", "Chưa tốt", "Đạt", "Tốt", "Rất tốt"];

    private readonly IUseCaseDispatcher _dispatcher;
    private readonly IClock _clock;
    private readonly PlanResultFormDto _form;
    private DispatcherTimer? _timer;
    private DateTime _timerStartedUtc;
    private LogResultDto? _saved;

    private PlanResultViewModel(IUseCaseDispatcher dispatcher, IClock clock, PlanResultFormDto form, LogResultDto? existing)
    {
        _dispatcher = dispatcher;
        _clock = clock;
        _form = form;
        Criteria = new(form.Criteria.Select(c => new CriterionItem(c.Id, c.Title, c.IsDone)));

        if (form.Log is { } log)
        {
            Actual = log.ActualValue;
            Done = log.ActualValue > 0;
            Quality = log.Quality;
            Note = form.Note;
        }
        else
        {
            Actual = form.PlanType is PlanType.Time or PlanType.Quantity ? form.TargetValue : 1; // Actual defaults to Target
            Done = true;
            Quality = 4;
        }

        if (existing is not null)
            ShowResult(existing);
    }

    /// <summary>Opens the form for a new result, or the saved result when the session is already logged.</summary>
    public static async Task<PlanResultViewModel?> OpenAsync(IUseCaseDispatcher dispatcher, IClock clock, Guid occurrenceId)
    {
        var form = await dispatcher.QueryAsync(new GetPlanResultFormQuery(occurrenceId));
        if (form is null)
            return null;
        var existing = form.Log is { } log ? await dispatcher.QueryAsync(new GetLogResultQuery(log.PlanLogId)) : null;
        return new PlanResultViewModel(dispatcher, clock, form, existing);
    }

    public override string Title => _form.PlanName;
    public string Subtitle => string.Join(" • ", new[] { _form.AreaName, _form.GoalName }.Where(s => !string.IsNullOrEmpty(s)));
    public string AreaColorHex => _form.AreaColorHex;
    public string DifficultyText => $"{Display.Difficulty(_form.Difficulty)} (×{Display.Number(_form.XpMultiplier)})";
    public bool CanLog => _form.CanLog;
    public bool IsEdit => _form.Log is not null;

    // ── Step ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormStep))]
    public partial bool IsResultStep { get; private set; }

    public bool IsFormStep => !IsResultStep;

    // ── Inputs by plan type (BRD §5.2) ──
    public bool UsesAmount => _form.PlanType is PlanType.Time or PlanType.Quantity;
    public bool UsesDoneToggle => _form.PlanType is PlanType.Binary or PlanType.Frequency;
    public bool UsesCriteria => _form.PlanType == PlanType.Achievement;
    public bool UsesTimer => _form.PlanType == PlanType.Time;

    public string AmountLabel => _form.PlanType == PlanType.Time ? "Thời gian thực tế" : "Số lượng thực tế";
    public string UnitText => _form.TargetUnit ?? (_form.PlanType == PlanType.Time ? "phút" : "lần");
    public string TargetText => $"Mục tiêu: {Display.Number(_form.TargetValue)} {UnitText}";
    private decimal Step => _form.PlanType == PlanType.Time ? 5 : 1;

    public ObservableCollection<CriterionItem> Criteria { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountProgress))]
    public partial decimal Actual { get; set; }

    /// <summary>How much of the target the current Actual covers, 0..100, for the bar under the stepper.</summary>
    public double AmountProgress => _form.TargetValue <= 0 ? 0 : (double)Math.Min(100, Actual / _form.TargetValue * 100);

    [ObservableProperty]
    public partial bool Done { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityText))]
    public partial int Quality { get; set; }

    public string QualityText => Quality is >= 1 and <= 5 ? $"{QualityLabels[Quality]} ({Quality}/5 sao)" : "";

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimerButtonText))]
    public partial bool IsTimerRunning { get; private set; }

    [ObservableProperty]
    public partial string ElapsedText { get; private set; } = "00:00";

    public string TimerButtonText => IsTimerRunning ? "Dừng & điền thời gian" : "Bắt đầu bấm giờ";

    [RelayCommand]
    private void Increase() => Actual += Step;

    [RelayCommand]
    private void Decrease() => Actual = Math.Max(0, Actual - Step);

    [RelayCommand]
    private void SetDone(bool done) => Done = done;

    /// <summary>Stopwatch for Time plans: stopping fills Actual with the elapsed minutes (rounded up).</summary>
    [RelayCommand]
    private void ToggleTimer()
    {
        if (IsTimerRunning)
        {
            StopTimer();
            Actual = Math.Max(1, (decimal)Math.Ceiling((_clock.UtcNow - _timerStartedUtc).TotalMinutes));
            return;
        }
        _timerStartedUtc = _clock.UtcNow;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateElapsed());
        _timer.Start();
        IsTimerRunning = true;
        UpdateElapsed();
    }

    private void UpdateElapsed()
    {
        var elapsed = _clock.UtcNow - _timerStartedUtc;
        ElapsedText = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
        IsTimerRunning = false;
    }

    [RelayCommand]
    private Task Submit()
    {
        if (!CanLog)
        {
            ErrorMessage = "Không thể ghi nhận cho ngày trong tương lai.";
            return Task.CompletedTask;
        }
        StopTimer();
        var entry = new LogEntry(
            Actual: UsesAmount ? Actual : UsesDoneToggle ? (Done ? 1 : 0) : 0,
            Done: UsesAmount ? Actual > 0 : UsesDoneToggle ? Done : Criteria.Any(c => c.IsDone),
            Quality, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
            UsesCriteria ? Criteria.Where(c => c.IsDone).Select(c => c.Id).ToList() : null);

        Guid? logId = _saved?.PlanLogId ?? _form.Log?.PlanLogId;
        return SubmitStepAsync(logId is Guid id
            ? () => _dispatcher.SendAsync(new EditPlanLogCommand(id, entry))
            : () => _dispatcher.SendAsync(new LogPlanResultCommand(_form.OccurrenceId, entry)));
    }

    private async Task SubmitStepAsync(Func<Task<Domain.Common.Result<LogResultDto>>> send)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await send();
            if (result.IsSuccess)
                ShowResult(result.Value!);
            else
                ErrorMessage = ErrorMessages.For(result.Error!);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Result step ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Score), nameof(ScoreLabel), nameof(ScoreBrush), nameof(XpText), nameof(Breakdown),
                              nameof(AreaLevelText), nameof(AreaLevelProgress), nameof(NextLevelText), nameof(CanEdit),
                              nameof(EditHint), nameof(StreakText))]
    public partial LogResultDto? Result { get; private set; }

    public int Score => Result?.Score ?? 0;
    public string ScoreLabel => Result is null ? "" : Display.ScoreTier(Result.Score).Label.ToUpperInvariant();
    public IBrush ScoreBrush => Display.Brush(Result is null ? "#94A3B8" : Display.ScoreTier(Result.Score).ColorHex);
    public string XpText => Result is null ? "" : $"+{Display.Number(Result.Xp)} XP";
    public bool CanEdit => Result?.CanEdit == true;
    public string EditHint => Result is null ? "" : $"Sửa được đến {LocalTime(Result.EditableUntil):HH:mm dd/MM}";
    public string StreakText => Result is null ? "" : $"Chuỗi {Result.CurrentStreak} ngày";

    public IReadOnlyList<BreakdownItem> Breakdown => Result is null ? [] : BuildBreakdown(Result);

    public string AreaLevelText => Result is null ? ""
        : $"{Result.AreaName}: Level {Result.AreaLevel.Level} · {Display.Number(Result.AreaLevel.TotalXp)} XP";
    public double AreaLevelProgress => Result?.AreaLevel is { NextLevelXp: { } next } level
        ? (double)((level.TotalXp - level.LevelStartXp) / (next - level.LevelStartXp) * 100)
        : 100;
    public string NextLevelText => Result?.AreaLevel is { XpToNextLevel: { } remaining } level
        ? $"Còn {Display.Number(remaining)} XP lên Level {level.Level + 1}"
        : "Đã đạt Level tối đa";

    [RelayCommand]
    private void EditAgain()
    {
        if (CanEdit)
            IsResultStep = false;
    }

    [RelayCommand]
    private void Finish() => Close(_saved);

    /// <summary>Closing after a save still reports the result, so the caller refreshes.</summary>
    protected override void CloseWithoutResult()
    {
        StopTimer();
        Close(_saved);
    }

    private void ShowResult(LogResultDto result)
    {
        _saved = result;
        Result = result;
        IsResultStep = true;
    }

    private DateTime LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _clock.TimeZone);

    private IReadOnlyList<BreakdownItem> BuildBreakdown(LogResultDto r)
    {
        string Pct(decimal weight) => $"{weight * 100:0}%";
        string actualDetail = _form.PlanType switch
        {
            PlanType.Time or PlanType.Quantity => $"Thực tế {Display.Number(Actual)} / {Display.Number(_form.TargetValue)} {UnitText}",
            PlanType.Achievement => $"{Criteria.Count(c => c.IsDone)} / {Criteria.Count} tiêu chí",
            _ => Done ? "Đã thực hiện" : "Không thực hiện",
        };
        var items = new List<BreakdownItem>
        {
            new($"Hoàn thành ({Pct(r.Weights.Completion)})", Display.Percent(r.Completion), actualDetail, (double)r.Completion, Display.Brush("#2563EB")),
            new($"Chất lượng ({Pct(r.Weights.Quality)})", Display.Percent(r.Quality), $"Tự chấm {Quality} / 5 sao", (double)r.Quality, Display.Brush("#0D9488")),
            new($"Nhất quán ({Pct(r.Weights.Consistency)})", Display.Percent(r.Consistency), "Số buổi làm / kế hoạch trong 7 ngày", (double)r.Consistency, Display.Brush("#7C3AED")),
        };
        // Under V2 Difficulty only multiplies XP; under V1 it is also a 15% Score component.
        items.Add(r.Weights.Difficulty > 0
            ? new($"Độ khó ({Pct(r.Weights.Difficulty)})", Display.Percent(r.DifficultyPoints), $"{Display.Difficulty(r.Difficulty)} · XP ×{Display.Number(r.XpMultiplier)}", (double)r.DifficultyPoints, Display.Brush("#0F172A"))
            : new("Độ khó (hệ số XP)", $"×{Display.Number(r.XpMultiplier)}", $"{Display.Difficulty(r.Difficulty)} · không tính vào Score", 100, Display.Brush("#0F172A")));
        return items;
    }
}
