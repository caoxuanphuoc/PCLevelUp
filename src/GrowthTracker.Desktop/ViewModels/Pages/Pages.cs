namespace GrowthTracker.Desktop.ViewModels.Pages;

// Placeholder pages for MVP screens not built yet (TDD §6.1). Each gets its own file once its sprint starts.

public sealed class DashboardViewModel : PageViewModel
{
    public override string Title => "Tổng quan";
    public override string Subtitle => "Tổng quan Score, XP, Level và xu hướng — Sprint 5";
}


public sealed class CalendarViewModel : PageViewModel
{
    public override string Title => "Lịch biểu";
    public override string Subtitle => "Tháng / Tuần / Danh sách, kéo thả dời lịch — Sprint 5";
}

public sealed class WeeklyReviewViewModel : PageViewModel
{
    public override string Title => "Đánh giá tuần";
    public override string Subtitle => "Tổng kết tuần và gợi ý điều chỉnh — Sprint 6";
}
