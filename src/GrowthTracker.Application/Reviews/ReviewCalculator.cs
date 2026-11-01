using System.Globalization;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Logging;
using GrowthTracker.Domain.Gamification;
using GrowthTracker.Domain.Goals;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Progress;
using GrowthTracker.Domain.Reviews;
using GrowthTracker.Domain.Risk;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Reviews;

public enum AreaReviewStatus
{
    OnTrack,
    Excellent,
    NeedsAttention,
}

public sealed record AreaReviewDto(Guid AreaId, string Name, string ColorHex, decimal? Score, decimal? PreviousScore, Trend? Trend,
                                   AreaReviewStatus Status);

/// <summary>A highlight or an attention item (UI Design §8), with what it points to.</summary>
public sealed record ReviewNoteDto(string Title, string Detail, Guid? PlanId = null, Guid? GoalId = null, Guid? AreaId = null);

/// <summary>The figures of one week (Mon–Sun), computed from sessions and logs.</summary>
internal sealed record WeekFigures(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    bool IsProvisional,
    decimal? Score,
    decimal? PreviousScore,
    Trend? Trend,
    int PlansDone,
    int PlansTotal,
    decimal ConsistencyPct,
    decimal? PreviousConsistencyPct,
    decimal XpGained,
    decimal PreviousXpGained,
    int Streak,
    int ActiveDays,
    int LoggedDays,
    IReadOnlyList<AreaReviewDto> Areas,
    IReadOnlyList<ReviewNoteDto> Highlights,
    IReadOnlyList<ReviewNoteDto> Attention);

/// <summary>A target change the Recommender proposes for one plan (FR-52, FR-53).</summary>
internal sealed record SuggestionCandidate(Guid PlanId, RecommendationKind Kind, decimal Current, decimal Suggested, string Reason);

/// <summary>
/// Builds the Weekly Review (FR-40 → FR-43, UI Design §8) from live data, so a report always reflects
/// edits made within 24 hours. The stored WeeklyReview row keeps a snapshot, the note and the completion.
/// </summary>
internal sealed class ReviewCalculator(IAppDbContext db, IClock clock, RiskDetector risk, Recommender recommender, DomainThresholds thresholds)
{
    public const int MaxNotes = 3;
    public const int MinLoggedDaysForSuggestions = 3;

    public static DateOnly WeekStartOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public async Task<WeekFigures> FiguresAsync(DateOnly weekStart, CancellationToken ct)
    {
        var today = clock.Today;
        var weekEnd = weekStart.AddDays(6);
        bool provisional = today <= weekEnd;
        var countUntil = provisional ? today : weekEnd;
        var previousStart = weekStart.AddDays(-7);

        var areas = await db.Areas.IgnoreQueryFilters().ToDictionaryAsync(a => a.Id, ct);
        var goals = await db.Goals.ToListAsync(ct);
        var goalArea = goals.ToDictionary(g => g.Id, g => g.AreaId);
        var links = await db.PlanGoalLinks.ToListAsync(ct);
        var plans = await db.Plans.IgnoreQueryFilters().ToDictionaryAsync(p => p.Id, ct);
        Guid? AreaOf(Guid planId) => links.Where(l => l.PlanId == planId && goalArea.ContainsKey(l.GoalId))
                                          .OrderByDescending(l => l.Weight).Select(l => (Guid?)goalArea[l.GoalId]).FirstOrDefault();

        // Sessions of this and the previous week, with their logs.
        var sessions = await db.PlanOccurrences.Where(o => o.ScheduledDate >= previousStart && o.ScheduledDate <= weekEnd)
                                               .Select(o => new { o.Id, o.PlanId, o.ScheduledDate, o.State }).ToListAsync(ct);
        var ids = sessions.Select(s => s.Id).ToList();
        var logs = await db.PlanLogs.Where(l => ids.Contains(l.OccurrenceId))
                                    .Select(l => new { l.OccurrenceId, l.Score, l.Xp }).ToDictionaryAsync(l => l.OccurrenceId, ct);

        var thisWeek = sessions.Where(s => s.ScheduledDate >= weekStart && s.ScheduledDate <= countUntil).ToList();
        var lastWeek = sessions.Where(s => s.ScheduledDate >= previousStart && s.ScheduledDate < weekStart).ToList();

        decimal? AverageScore(IEnumerable<Guid> sessionIds)
        {
            var scores = sessionIds.Where(logs.ContainsKey).Select(id => logs[id].Score).ToList();
            return scores.Count == 0 ? null : Math.Round((decimal)scores.Average(), 1);
        }
        decimal ConsistencyOf<T>(List<T> list, Func<T, OccurrenceState> state) =>
            list.Count == 0 ? 0 : Math.Round(list.Count(x => state(x) == OccurrenceState.Done) * 100m / list.Count, 1);
        Trend? Compare(decimal? now, decimal? before) =>
            now is { } n && before is { } b ? TrendClassifier.Compare(n, b, thresholds.TrendThresholdPoints) : null;

        var score = AverageScore(thisWeek.Select(s => s.Id));
        var previousScore = AverageScore(lastWeek.Select(s => s.Id));

        // Areas (FR-41): this week vs last week.
        var openGoals = goals.Where(g => !g.IsClosed).ToList();
        var areaRows = sessions.Select(s => AreaOf(s.PlanId)).Where(a => a is Guid id && areas.ContainsKey(id)).Select(a => a!.Value).Distinct()
            .OrderBy(id => areas[id].SortOrder)
            .Select(id =>
            {
                var now = AverageScore(thisWeek.Where(s => AreaOf(s.PlanId) == id).Select(s => s.Id));
                var before = AverageScore(lastWeek.Where(s => AreaOf(s.PlanId) == id).Select(s => s.Id));
                var trend = Compare(now, before);
                bool atRisk = openGoals.Any(g => g.AreaId == id && g.Status == GoalStatus.AtRisk);
                var status = now >= 90 ? AreaReviewStatus.Excellent
                           : trend == Trend.Declining || now < 60 || atRisk ? AreaReviewStatus.NeedsAttention
                           : AreaReviewStatus.OnTrack;
                return new AreaReviewDto(id, areas[id].Name, areas[id].ColorHex, now, before, trend, status);
            })
            .ToList();

        // Highlights: improving Areas, excellent Areas, plans done every session of the week.
        var highlights = new List<ReviewNoteDto>();
        foreach (var a in areaRows.Where(a => a.Trend == Trend.Improving).OrderByDescending(a => a.Score - a.PreviousScore))
            highlights.Add(new($"{a.Name} tăng {Points(a.Score!.Value - a.PreviousScore!.Value)} điểm",
                               $"Từ {Points(a.PreviousScore.Value)} lên {Points(a.Score.Value)} so với tuần trước.", AreaId: a.AreaId));
        foreach (var a in areaRows.Where(a => a.Status == AreaReviewStatus.Excellent && a.Trend != Trend.Improving))
            highlights.Add(new($"{a.Name} đạt {Points(a.Score!.Value)} điểm", "Giữ vững mức Xuất sắc trong tuần.", AreaId: a.AreaId));
        foreach (var g in thisWeek.GroupBy(s => s.PlanId).Where(g => g.Count() >= 2 && g.All(s => s.State == OccurrenceState.Done)))
            highlights.Add(new($"{plans[g.Key].Name}: {g.Count()}/{g.Count()} buổi", "Hoàn thành mọi buổi đã lên lịch trong tuần.", PlanId: g.Key));

        // Attention: declining Areas, plans that may be too hard (FR-51), Goals at risk (FR-50).
        var attention = new List<ReviewNoteDto>();
        foreach (var a in areaRows.Where(a => a.Trend == Trend.Declining).OrderBy(a => a.Score - a.PreviousScore))
            attention.Add(new($"{a.Name} giảm {Points(a.PreviousScore!.Value - a.Score!.Value)} điểm",
                              $"Từ {Points(a.PreviousScore.Value)} xuống {Points(a.Score.Value)} so với tuần trước.", AreaId: a.AreaId));
        foreach (var hard in await PlansTooHardAsync(countUntil, ct))
            attention.Add(new($"{plans[hard.PlanId].Name} có thể quá khó",
                              $"Chỉ đạt {hard.Done}/{hard.Scheduled} buổi trong 14 ngày ({Points(hard.Pct)}%).", PlanId: hard.PlanId));
        foreach (var g in openGoals.Where(g => g.Status == GoalStatus.AtRisk).OrderBy(g => g.Progress - g.ExpectedProgress))
            attention.Add(new($"{g.Name} đang chậm",
                              $"Tiến độ {Points(g.Progress)}%, kỳ vọng {Points(ProgressCalculator.Expected(g.StartDate, g.TargetDate, today))}%.", GoalId: g.Id));

        var doneDays = await XpTotals.DoneDaysAsync(db, countUntil, ct);
        return new WeekFigures(
            weekStart, weekEnd, provisional, score, previousScore, Compare(score, previousScore),
            thisWeek.Count(s => s.State == OccurrenceState.Done),
            thisWeek.Count(s => s.State != OccurrenceState.Skipped),
            ConsistencyOf(thisWeek, s => s.State),
            lastWeek.Count == 0 ? null : ConsistencyOf(lastWeek, s => s.State),
            thisWeek.Where(s => logs.ContainsKey(s.Id)).Sum(s => logs[s.Id].Xp),
            lastWeek.Where(s => logs.ContainsKey(s.Id)).Sum(s => logs[s.Id].Xp),
            StreakCalculator.Calculate(doneDays, [], countUntil).Current,
            thisWeek.Where(s => s.State == OccurrenceState.Done).Select(s => s.ScheduledDate).Distinct().Count(),
            thisWeek.Where(s => logs.ContainsKey(s.Id)).Select(s => s.ScheduledDate).Distinct().Count(),
            areaRows, highlights.Take(MaxNotes).ToList(), attention.Take(MaxNotes).ToList());
    }

    /// <summary>FR-51: plans whose Consistency over the 14 days ending <paramref name="until"/> is below 50%.</summary>
    public async Task<IReadOnlyList<(Guid PlanId, int Done, int Scheduled, decimal Pct)>> PlansTooHardAsync(DateOnly until, CancellationToken ct)
    {
        var from = until.AddDays(-(thresholds.PlanRiskWindowDays - 1));
        // A plan that ended before the window closes is history, not "too hard".
        var activeIds = await db.Plans.Where(p => p.Status == PlanStatus.Active && p.DurationEnd >= until).Select(p => p.Id).ToListAsync(ct);
        var window = await db.PlanOccurrences.Where(o => activeIds.Contains(o.PlanId) && o.ScheduledDate >= from && o.ScheduledDate <= until)
                                             .Select(o => new { o.PlanId, o.ScheduledDate, o.State }).ToListAsync(ct);
        var firstSession = (await db.PlanOccurrences.Where(o => activeIds.Contains(o.PlanId))
                                                    .GroupBy(o => o.PlanId)
                                                    .Select(g => new { PlanId = g.Key, First = g.Min(o => o.ScheduledDate) })
                                                    .ToListAsync(ct))
                           .ToDictionary(x => x.PlanId, x => x.First);
        return window.GroupBy(o => o.PlanId)
            .Select(g =>
            {
                int days = until.DayNumber - firstSession[g.Key].DayNumber + 1;
                var r = risk.EvaluatePlan(g.Count(o => o.State == OccurrenceState.Done), g.Count(), Math.Min(days, thresholds.PlanRiskWindowDays));
                return (PlanId: g.Key, Done: g.Count(o => o.State == OccurrenceState.Done), Scheduled: g.Count(), Pct: r.ConsistencyPct, r.IsTooHard);
            })
            .Where(x => x.IsTooHard)
            .OrderBy(x => x.Pct)
            .Select(x => (x.PlanId, x.Done, x.Scheduled, Math.Round(x.Pct, 1)))
            .ToList();
    }

    /// <summary>
    /// Target suggestions for the 4 weeks ending <paramref name="weekEnd"/> (FR-52, FR-53). Plans with a pending
    /// suggestion, or one dismissed less than 4 weeks ago (FR-54), are left alone.
    /// </summary>
    public async Task<IReadOnlyList<SuggestionCandidate>> SuggestionsAsync(DateOnly weekEnd, CancellationToken ct)
    {
        var today = clock.Today;
        var until = weekEnd < today ? weekEnd : today;
        var from = until.AddDays(-(thresholds.RecommendationWindowWeeks * 7 - 1));
        // Only plans that still run after this week: a new target applies to future sessions only (BR-14).
        var plans = await db.Plans.Where(p => p.Status == PlanStatus.Active && p.DurationEnd > weekEnd
                                              && (p.PlanType == PlanType.Time || p.PlanType == PlanType.Quantity || p.PlanType == PlanType.Frequency))
                                  .ToListAsync(ct);
        var planIds = plans.Select(p => p.Id).ToList();
        var blocked = (await db.Recommendations.Where(r => planIds.Contains(r.PlanId)
                                                          && (r.State == RecommendationState.Pending
                                                              || (r.State == RecommendationState.Dismissed && r.SnoozedUntil >= today)))
                                               .Select(r => r.PlanId).ToListAsync(ct)).ToHashSet();
        var sessions = await db.PlanOccurrences.Where(o => planIds.Contains(o.PlanId) && o.ScheduledDate <= until)
                                               .Select(o => new { o.Id, o.PlanId, o.ScheduledDate, o.State }).ToListAsync(ct);
        var sessionIds = sessions.Where(s => s.ScheduledDate >= from).Select(s => s.Id).ToList();
        var completion = await db.PlanLogs.Where(l => sessionIds.Contains(l.OccurrenceId))
                                          .ToDictionaryAsync(l => l.OccurrenceId, l => l.Completion, ct);

        var result = new List<SuggestionCandidate>();
        foreach (var plan in plans.Where(p => !blocked.Contains(p.Id)))
        {
            var all = sessions.Where(s => s.PlanId == plan.Id).ToList();
            if (all.Count == 0)
                continue;
            var due = all.Where(s => s.ScheduledDate >= from).ToList();
            if (due.Count == 0)
                continue;
            // Completion rate: average completion of due sessions, missed and skipped counting as 0.
            decimal completionPct = due.Sum(s => completion.GetValueOrDefault(s.Id)) / due.Count;
            decimal consistencyPct = due.Count(s => s.State == OccurrenceState.Done) * 100m / due.Count;
            int daysOfData = until.DayNumber - all.Min(s => s.ScheduledDate).DayNumber + 1;
            decimal current = plan.PlanType == PlanType.Frequency ? plan.FrequencyPerWeek ?? 0 : plan.TargetValue;

            var suggestion = recommender.Suggest(new RecommendationInput(plan.PlanType, current, completionPct, consistencyPct, daysOfData, today));
            if (suggestion is null)
                continue;
            string reason = suggestion.Kind == RecommendationKind.DecreaseTarget
                ? $"Hoàn thành {Points(completionPct)}% trong 4 tuần gần nhất"
                : $"Nhất quán {Points(consistencyPct)}% trong 4 tuần gần nhất";
            result.Add(new SuggestionCandidate(plan.Id, suggestion.Kind, suggestion.CurrentValue, suggestion.SuggestedValue, reason));
        }
        return result;
    }

    private static string Points(decimal value) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"));
}
