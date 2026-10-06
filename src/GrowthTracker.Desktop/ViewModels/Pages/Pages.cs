namespace GrowthTracker.Desktop.ViewModels.Pages;

// Placeholder pages for the MVP screens (TDD §6.1). Each gets its own file once its sprint starts.

public sealed class DashboardViewModel : PageViewModel
{
    public override string Title => "Dashboard";
    public override string Subtitle => "Tổng quan Score, XP, Level và xu hướng — Sprint 5";
}

public sealed class TodayViewModel : PageViewModel
{
    public override string Title => "Hôm nay";
    public override string Subtitle => "Danh sách buổi Plan hôm nay và ghi nhận kết quả — Sprint 4";
}

public sealed class CalendarViewModel : PageViewModel
{
    public override string Title => "Lịch biểu";
    public override string Subtitle => "Tháng / Tuần / Danh sách, kéo thả dời lịch — Sprint 5";
}

public sealed class GoalsViewModel : PageViewModel
{
    public override string Title => "Goals";
    public override string Subtitle => "Mục tiêu, Milestone và Plan — Sprint 3";
}

public sealed class WeeklyReviewViewModel : PageViewModel
{
    public override string Title => "Weekly Review";
    public override string Subtitle => "Tổng kết tuần và gợi ý điều chỉnh — Sprint 6";
}

public sealed class SettingsViewModel : PageViewModel
{
    public override string Title => "Cài đặt";
    public override string Subtitle => "Area, giao diện, sao lưu và khôi phục";
}
