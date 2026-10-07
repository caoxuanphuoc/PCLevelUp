using System.Globalization;
using System.Text.Json;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Plans;
using GrowthTracker.Domain.Common;
using GrowthTracker.Domain.Plans;
using GrowthTracker.Domain.Progress;
using GrowthTracker.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace GrowthTracker.Application.Reviews;

// ── GetWeeklyReview ─────────────────────────────────────────────────────────

/// <param name="WeekStart">Monday of the week; null opens the default week (last week, or this week on Sunday).</param>
public sealed record GetWeeklyReviewQuery(DateOnly? WeekStart = null) : IQuery<WeeklyReviewDto>;

public sealed record SuggestionDto(Guid Id, Guid PlanId, string PlanName, PlanType PlanType, string? Unit, RecommendationKind Kind,
                                   decimal CurrentValue, decimal SuggestedValue, string Reason, RecommendationState State);

/// <param name="IsProvisional">The week has not ended yet: figures are "Tạm tính".</param>
/// <param name="IsGenerated">The report exists (created on Sunday 20:00 or when completed); suggestions belong to it.</param>
/// <param name="CanSuggest">Enough data for suggestions: at least 3 days with a log in the week (UI Design §8).</param>
public sealed record WeeklyReviewDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int IsoWeek,
    bool IsProvisional,
    bool IsGenerated,
    bool IsCompleted,
    string? Note,
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
    bool CanSuggest,
    bool CanGoNext,
    IReadOnlyList<AreaReviewDto> Areas,
    IReadOnlyList<ReviewNoteDto> Highlights,
    IReadOnlyList<ReviewNoteDto> Attention,
    IReadOnlyList<SuggestionDto> Suggestions);

internal sealed class GetWeeklyReviewHandler(IAppDbContext db, IClock clock, ReviewCalculator calculator)
    : IQueryHandler<GetWeeklyReviewQuery, WeeklyReviewDto>
{
    public async Task<WeeklyReviewDto> HandleAsync(GetWeeklyReviewQuery q, CancellationToken ct)
    {
        var today = clock.Today;
        var weekStart = ReviewCalculator.WeekStartOf(q.WeekStart ?? ReviewWeeks.Default(today));
        var figures = await calculator.FiguresAsync(weekStart, ct);
        var review = await db.WeeklyReviews.FirstOrDefaultAsync(r => r.WeekStart == weekStart, ct);

        var suggestions = new List<SuggestionDto>();
        if (review is not null)
        {
            var rows = await db.Recommendations.Where(r => r.ReviewId == review.Id).ToListAsync(ct);
            var planIds = rows.Select(r => r.PlanId).ToList();
            var plans = await db.Plans.IgnoreQueryFilters().Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            suggestions = rows.Where(r => plans.ContainsKey(r.PlanId))
                .OrderBy(r => r.Kind)
                .Select(r => new SuggestionDto(r.Id, r.PlanId, plans[r.PlanId].Name, plans[r.PlanId].PlanType, plans[r.PlanId].TargetUnit,
                                               r.Kind, r.CurrentValue, r.SuggestedValue, r.Reason, r.State))
                .ToList();
        }

        return new WeeklyReviewDto(
            weekStart, figures.WeekEnd, ISOWeek.GetWeekOfYear(weekStart.ToDateTime(TimeOnly.MinValue)),
            figures.IsProvisional, review is not null, review?.IsCompleted == true, review?.Note,
            figures.Score, figures.PreviousScore, figures.Trend, figures.PlansDone, figures.PlansTotal,
            figures.ConsistencyPct, figures.PreviousConsistencyPct, figures.XpGained, figures.PreviousXpGained,
            figures.Streak, figures.ActiveDays,
            CanSuggest: figures.LoggedDays >= ReviewCalculator.MinLoggedDaysForSuggestions,
            CanGoNext: weekStart < ReviewCalculator.WeekStartOf(today),
            figures.Areas, figures.Highlights, figures.Attention, suggestions);
    }
}

// ── GenerateWeeklyReview ────────────────────────────────────────────────────

/// <summary>
/// Creates (or refreshes) the stored report for a week (FR-40) and its target suggestions (FR-52, FR-53).
/// Run by the Sunday 20:00 job and when a review is completed. Returns the review id.
/// </summary>
public sealed record GenerateWeeklyReviewCommand(DateOnly WeekStart) : ICommand<Guid>;

internal sealed class GenerateWeeklyReviewHandler(IAppDbContext db, ReviewCalculator calculator)
    : ICommandHandler<GenerateWeeklyReviewCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(GenerateWeeklyReviewCommand c, CancellationToken ct) =>
        Result<Guid>.Success((await ReviewWeeks.GenerateAsync(db, calculator, ReviewCalculator.WeekStartOf(c.WeekStart), ct)).Id);
}

// ── Note / Complete ─────────────────────────────────────────────────────────

/// <summary>Saves the free-text note of the week (FR-43); completing the review is separate.</summary>
public sealed record SaveWeeklyReviewNoteCommand(DateOnly WeekStart, string? Note) : ICommand<Guid>;

internal sealed class SaveWeeklyReviewNoteHandler(IAppDbContext db, ReviewCalculator calculator)
    : ICommandHandler<SaveWeeklyReviewNoteCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(SaveWeeklyReviewNoteCommand c, CancellationToken ct)
    {
        var review = await ReviewWeeks.GenerateAsync(db, calculator, ReviewCalculator.WeekStartOf(c.WeekStart), ct);
        review.SaveNote(c.Note);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(review.Id);
    }
}

/// <summary>"Hoàn tất review" (FR-43): marks the week reviewed, which hides the Dashboard reminder.</summary>
public sealed record CompleteWeeklyReviewCommand(DateOnly WeekStart, string? Note) : ICommand<Guid>;

internal sealed class CompleteWeeklyReviewHandler(IAppDbContext db, IClock clock, ReviewCalculator calculator)
    : ICommandHandler<CompleteWeeklyReviewCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CompleteWeeklyReviewCommand c, CancellationToken ct)
    {
        var review = await ReviewWeeks.GenerateAsync(db, calculator, ReviewCalculator.WeekStartOf(c.WeekStart), ct);
        review.Complete(c.Note, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(review.Id);
    }
}

// ── Apply / Dismiss suggestion ──────────────────────────────────────────────

/// <summary>
/// Applies a confirmed suggestion (FR-54, BR-14): the plan's target changes for future sessions only.
/// A new sessions-per-week count reschedules the sessions after today.
/// </summary>
public sealed record ApplyRecommendationCommand(Guid RecommendationId) : ICommand<Guid>;

internal sealed class ApplyRecommendationHandler(IAppDbContext db, IClock clock) : ICommandHandler<ApplyRecommendationCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ApplyRecommendationCommand c, CancellationToken ct)
    {
        var recommendation = await db.Recommendations.FirstOrDefaultAsync(r => r.Id == c.RecommendationId, ct);
        if (recommendation is null)
            return Result<Guid>.Failure(Errors.NotFound("recommendation"));
        if (recommendation.State != RecommendationState.Pending)
            return Result<Guid>.Failure("recommendation.not_pending", "This suggestion was already handled.");
        var plan = await db.Plans.Include(p => p.GoalLinks).Include(p => p.Criteria).FirstOrDefaultAsync(p => p.Id == recommendation.PlanId, ct);
        if (plan is null || plan.Status != PlanStatus.Active)
            return Result<Guid>.Failure("plan.not_active", "Only active plans can be changed.");

        var today = clock.Today;
        plan.UpdateTarget(recommendation.SuggestedValue);
        recommendation.MarkApplied();

        if (plan.PlanType == PlanType.Frequency)
        {
            var future = await db.PlanOccurrences.Where(o => o.PlanId == plan.Id && o.State == OccurrenceState.Scheduled && o.ScheduledDate > today)
                                                 .ToListAsync(ct);
            foreach (var session in future)
                session.SoftDelete();
            var kept = await db.PlanOccurrences.Where(o => o.PlanId == plan.Id && o.ScheduledDate >= today)
                                               .Select(o => o.ScheduledDate).ToListAsync(ct);
            var taken = kept.Except(future.Select(f => f.ScheduledDate)).Append(today).ToHashSet(); // today stays as it is
            db.PlanOccurrences.AddRange(OccurrencePlanner.TopUp(plan, taken, today));
        }

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(plan.Id);
    }
}

/// <summary>"Bỏ qua" hides the suggestion and suppresses new ones for that plan for 4 weeks (FR-54).</summary>
public sealed record DismissRecommendationCommand(Guid RecommendationId) : ICommand<Guid>;

internal sealed class DismissRecommendationHandler(IAppDbContext db, IClock clock, Domain.Risk.DomainThresholds thresholds)
    : ICommandHandler<DismissRecommendationCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(DismissRecommendationCommand c, CancellationToken ct)
    {
        var recommendation = await db.Recommendations.FirstOrDefaultAsync(r => r.Id == c.RecommendationId, ct);
        if (recommendation is null)
            return Result<Guid>.Failure(Errors.NotFound("recommendation"));
        if (recommendation.State != RecommendationState.Pending)
            return Result<Guid>.Failure("recommendation.not_pending", "This suggestion was already handled.");
        recommendation.Dismiss(clock.Today, thresholds.RecommendationSnoozeWeeks);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(recommendation.Id);
    }
}

internal static class ReviewWeeks
{
    public static readonly TimeOnly GenerationTime = new(20, 0);

    /// <summary>On Sunday the current week is reviewed; on other days, last week.</summary>
    public static DateOnly Default(DateOnly today) =>
        today.DayOfWeek == DayOfWeek.Sunday ? ReviewCalculator.WeekStartOf(today) : ReviewCalculator.WeekStartOf(today).AddDays(-7);

    /// <summary>Upserts the stored report and adds suggestions when there is enough data.</summary>
    public static async Task<WeeklyReview> GenerateAsync(IAppDbContext db, ReviewCalculator calculator, DateOnly weekStart, CancellationToken ct)
    {
        var f = await calculator.FiguresAsync(weekStart, ct);
        string snapshot = JsonSerializer.Serialize(f.Areas.Select(a => new { a.AreaId, a.Name, a.Score, a.PreviousScore, Trend = a.Trend?.ToString() }));
        int score = f.Score is { } s ? (int)Math.Round(s, MidpointRounding.AwayFromZero) : 0;

        var review = await db.WeeklyReviews.FirstOrDefaultAsync(r => r.WeekStart == weekStart, ct);
        if (review is null)
        {
            review = new WeeklyReview(weekStart, score, f.PlansDone, f.PlansTotal, f.ConsistencyPct, f.XpGained, snapshot);
            db.WeeklyReviews.Add(review);
        }
        else
        {
            review.Refresh(score, f.PlansDone, f.PlansTotal, f.ConsistencyPct, f.XpGained, snapshot);
        }

        if (f.LoggedDays >= ReviewCalculator.MinLoggedDaysForSuggestions)
            foreach (var s2 in await calculator.SuggestionsAsync(f.WeekEnd, ct))
                db.Recommendations.Add(new Recommendation(s2.PlanId, s2.Kind, s2.Current, s2.Suggested, s2.Reason, review.Id));

        await db.SaveChangesAsync(ct);
        return review;
    }
}
