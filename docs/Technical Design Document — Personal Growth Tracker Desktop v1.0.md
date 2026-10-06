# Technical Design Document — Personal Growth Tracker Desktop v1.0

Oct 6, 2026 · @Alex

## 1. Tổng quan và lựa chọn công nghệ

**Quyết định:** xây ứng dụng desktop bằng **.NET 10 (LTS) + Avalonia UI 12**, dữ liệu lưu cục bộ trong **SQLite** qua **EF Core**, đóng gói và cập nhật tự động bằng **Velopack**. Một codebase C# chạy trên Windows 10/11 (x64, arm64) và Linux (x64; Ubuntu 22.04+, Fedora 39+), có thể thêm macOS sau mà không đổi kiến trúc.

### 1.1. So sánh phương án

| Tiêu chí | Avalonia UI (.NET) | Tauri (Rust + Web) | Electron (Node + Web) | .NET MAUI |
| --- | --- | --- | --- | --- |
| Windows + Linux | Có, chính thức | Có | Có | Linux không được hỗ trợ chính thức |
| Ngôn ngữ | C# / XAML | Rust + HTML/CSS/JS | JS/TS + HTML/CSS | C# / XAML |
| Kích thước bộ cài | Khoảng 40–70 MB (self-contained) | Nhỏ nhất, khoảng 5–15 MB | Lớn, khoảng 100–150 MB | — |
| RAM khi chạy | Thấp – trung bình | Thấp | Cao | — |
| Giao diện giống hệt nhau trên mọi OS | Có (tự vẽ bằng Skia) | Phụ thuộc WebView của OS (WebView2 / WebKitGTK) | Có (Chromium đóng gói kèm) | — |
| Phù hợp người làm .NET | Rất cao | Thấp | Thấp | Cao |
| Score Engine, EF Core, test dùng chung | Cùng một ngôn ngữ | Phải viết lại bằng Rust hoặc TS | Phải viết bằng TS | — |

**Lý do chọn Avalonia:** chạy được cả Windows lẫn Linux với một codebase C#; toàn bộ domain (Score Engine, Progress, Risk) viết bằng C# và test bằng xUnit; có thể tái dùng domain cho bản Web / API sau này (ASP.NET Core). Số liệu kích thước là ước lượng, cần đo lại khi build bản đầu tiên.

### 1.2. Stack chi tiết

| Lớp | Công nghệ | Ghi chú |
| --- | --- | --- |
| Runtime | .NET 10 LTS | Hỗ trợ đến 14/11/2028; .NET 8 và 9 hết hỗ trợ ngày 10/11/2026 |
| UI | Avalonia 12.x, theme Fluent | Bản 12.1.x phát hành tháng 7–8/2026 |
| MVVM | CommunityToolkit.Mvvm | Source generator cho ObservableProperty, RelayCommand |
| DI, cấu hình, logging | Microsoft.Extensions.Hosting | Generic Host dùng chung cho app và test |
| Dữ liệu | SQLite + EF Core 10 (Microsoft.EntityFrameworkCore.Sqlite) | Migration bằng EF Core tools |
| Biểu đồ | LiveCharts2 (SkiaSharp) | Đường xu hướng, Score Ring |
| Logging | Serilog, ghi file xoay vòng |  |
| Kiểm thử | xUnit, FluentAssertions, Avalonia.Headless | Test UI không cần màn hình |
| Đóng gói, cập nhật | Velopack | Bộ cài, delta update, cùng một lệnh cho Windows và Linux |
| CI/CD | GitHub Actions (windows-latest, ubuntu-latest) |  |

Nguồn: [Avalonia trên NuGet](https://www.nuget.org/profiles/AvaloniaUI) · [.NET Support Policy](https://dotnet.microsoft.com/en-US/platform/support/policy) · [Velopack](https://github.com/velopack/velopack) · [Velopack docs](https://docs.velopack.io/packaging/installer)

## 2. Kiến trúc

Ứng dụng theo **Clean Architecture** 4 tầng, phụ thuộc chỉ hướng vào trong: UI → Application → Domain; Infrastructure cài đặt các interface do Application định nghĩa. Domain không biết gì về Avalonia, SQLite hay hệ điều hành.

```text
┌──────────────────────────────────────────────────────────────┐
│ Desktop (Avalonia)                                            │
│  Views (AXAML) ── ViewModels (MVVM) ── Navigation, Theme      │
└──────────────────────────┬───────────────────────────────────┘
                           │ gọi Use Case (Command / Query)
┌──────────────────────────▼───────────────────────────────────┐
│ Application                                                   │
│  Use Cases: LogPlanResult, CreateGoal, ReschedulePlan, ...    │
│  Interfaces: IRepository, IClock, INotifier, IUnitOfWork      │
└───────────────┬──────────────────────────────┬───────────────┘
                │ dùng                         │ cài đặt bởi
┌───────────────▼──────────────┐  ┌────────────▼───────────────┐
│ Domain                        │  │ Infrastructure              │
│  Entities, Value Objects      │  │  EF Core + SQLite           │
│  ScoreEngine, ProgressCalc,   │  │  Notifier (Windows / Linux) │
│  RiskDetector, Recommender,   │  │  Backup, Velopack Updater   │
│  ScheduleGenerator            │  │  Serilog                    │
└───────────────────────────────┘  └─────────────────────────────┘
```

### 2.1. Nguyên tắc

| Nguyên tắc | Cách áp dụng |
| --- | --- |
| Local-first | Mọi dữ liệu nằm trong một file SQLite trên máy; app chạy đầy đủ khi không có mạng |
| Domain thuần | Score Engine và các bộ tính là hàm thuần (input → output), không truy cập DB, không gọi DateTime.Now; thời gian lấy qua IClock |
| Tính lại được | Score, XP, Progress lưu kèm phiên bản công thức (ScoreVersion); khi đổi công thức có thể tính lại toàn bộ lịch sử |
| CQRS nhẹ | Lệnh ghi qua Use Case; màn hình đọc qua Query trả về DTO dựng sẵn cho UI |
| Sự kiện domain | PlanLogged, PlanRescheduled, GoalStatusChanged; handler cập nhật XP, Progress, Streak trong cùng transaction |
| Sẵn sàng đồng bộ | Mọi bảng có Id dạng GUID v7, CreatedAt, UpdatedAt, IsDeleted (xoá mềm) để sau này đồng bộ cloud |

### 2.2. Luồng ghi nhận một Plan Result

1. Người dùng nhấn **Lưu kết quả** trong PlanResultViewModel.
2. ViewModel gửi lệnh LogPlanResultCommand (PlanOccurrenceId, Actual, Quality, Note).
3. Use Case nạp Plan, các Plan Log 7 ngày gần nhất, rồi gọi ScoreEngine.Calculate.
4. Tạo PlanLog với Score, các thành phần điểm, XP, ScoreVersion; phát sự kiện PlanLogged.
5. Handler cộng XP vào Area, tính lại Progress của Plan → Milestone → Goal, cập nhật Streak và trạng thái Risk.
6. Commit một transaction; trả về ScoreBreakdownDto để màn hình hiện Score, XP và Level.

## 3. Cấu trúc solution và module

Solution gồm 5 project chính và 4 project test; Domain không tham chiếu project nào khác.

```text
GrowthTracker.sln
├── src/
│   ├── GrowthTracker.Domain/            (không phụ thuộc thư viện ngoài)
│   │   ├── Areas/  Goals/  Milestones/  Plans/  PlanLogs/  Reviews/
│   │   ├── Scoring/        ScoreEngine.cs, ScoreWeights.cs, DifficultyTable.cs
│   │   ├── Progress/       ProgressCalculator.cs
│   │   ├── Gamification/   XpCalculator.cs, LevelTable.cs, StreakCalculator.cs
│   │   ├── Risk/           RiskDetector.cs, Recommender.cs
│   │   ├── Scheduling/     ScheduleGenerator.cs (Frequency → các buổi)
│   │   └── Common/         Entity, ValueObject, DomainEvent, Result<T>
│   ├── GrowthTracker.Application/       (tham chiếu Domain)
│   │   ├── Goals/  Plans/  Logging/  Calendar/  Dashboard/  Reviews/
│   │   ├── Abstractions/   IAppDbContext, IClock, INotifier, IBackupService
│   │   └── EventHandlers/
│   ├── GrowthTracker.Infrastructure/    (tham chiếu Application)
│   │   ├── Persistence/    AppDbContext, Configurations/, Migrations/
│   │   ├── Notifications/  WindowsToastNotifier, LinuxDbusNotifier
│   │   ├── Backup/  Updates/  Logging/  Jobs/
│   └── GrowthTracker.Desktop/           (Avalonia, điểm khởi động)
│       ├── App.axaml, Program.cs (Velopack + Host)
│       ├── Views/  ViewModels/  Controls/  Styles/  Assets/
│       └── Services/       NavigationService, ThemeService, DialogService
└── tests/
    ├── GrowthTracker.Domain.Tests/
    ├── GrowthTracker.Application.Tests/
    ├── GrowthTracker.Infrastructure.Tests/   (SQLite in-memory)
    └── GrowthTracker.Desktop.Tests/          (Avalonia.Headless)
```

### 3.1. Module theo tính năng

| Module | Use Case chính | Màn hình |
| --- | --- | --- |
| Goals | CreateGoal, UpdateGoal, AddMilestone, CancelGoal | Goals, Goal Detail |
| Plans | CreatePlan, UpdateTarget, ArchivePlan | Goal Detail |
| Logging | LogPlanResult, EditPlanLog, SkipOccurrence | Today, Plan Result |
| Calendar | GetMonthView, GetWeekView, ReschedulePlan, AddPlanToCalendar | Lịch biểu |
| Dashboard | GetDashboardSummary, GetScoreTrend | Dashboard |
| Reviews | GenerateWeeklyReview, CompleteReview, ApplyRecommendation | Weekly Review |
| Settings | ManageAreas, SetTheme, Backup, Restore | Settings |

Quy ước: mỗi Use Case là một cặp Command/Query + Handler trong cùng thư mục; không dùng MediatR để giảm phụ thuộc, handler được đăng ký trực tiếp vào DI.

## 4. Mô hình dữ liệu (SQLite)

14 bảng; điểm then chốt là tách **PlanOccurrence** (một buổi đã lên lịch) khỏi **PlanLog** (kết quả thực hiện buổi đó). Nhờ vậy Lịch biểu, dời lịch và Consistency đều tính được từ cùng dữ liệu.

```text
Area 1─* Goal 1─* Milestone
Vision 1─* Goal
Goal *─* Plan        (qua PlanGoalLink, có Weight)
Milestone 1─* Plan   (tuỳ chọn)
Plan 1─* PlanOccurrence 1─0..1 PlanLog
Plan 1─* AchievementCriterion
PlanLog 1─* XpLedgerEntry *─1 Area
WeeklyReview 1─* Recommendation *─1 Plan
```

### 4.1. Bảng chính

| Bảng | Cột chính | Ghi chú |
| --- | --- | --- |
| UserProfile | Id, DisplayName, PinHash?, Locale, TimeZone, WeekStartsOn, CreatedAt | Một dòng duy nhất trong MVP |
| Area | Id, Name, ColorHex, SortOrder, IsArchived | 6 dòng mẫu khi khởi tạo |
| Vision | Id, Title, Description |  |
| Goal | Id, AreaId, VisionId?, Name, Description, Priority (1–4), StartDate, TargetDate, TargetDescription, Status, Progress, ExpectedProgress, Score | Progress và Score là giá trị cache, tính lại được |
| Milestone | Id, GoalId, Name, DueDate?, Weight, SortOrder, Status, Progress |  |
| Plan | Id, MilestoneId?, Name, PlanType, TargetValue, TargetUnit, FrequencyPerWeek?, DaysOfWeek (bitmask), PreferredTime?, DurationStart, DurationEnd, Difficulty, Status | PlanType: Time, Quantity, Frequency, Binary, Achievement |
| PlanGoalLink | PlanId, GoalId, Weight | Khoá chính kép; BR-01: ít nhất một dòng cho mỗi Plan |
| AchievementCriterion | Id, PlanId, Title, IsDone, DoneAt? | Cho Plan loại Achievement |
| PlanOccurrence | Id, PlanId, ScheduledDate, ScheduledTime?, OriginalDate, State, RescheduleCount, RescheduleReason? | State: Scheduled, Done, Skipped, Missed |
| PlanLog | Id, OccurrenceId, ActualValue, Quality (1–5), Note, Completion, QualityPct, Consistency, DifficultyPts, Score, Xp, ScoreVersion, LoggedAt, EditableUntil | Lưu cả thành phần điểm để hiển thị và kiểm toán |
| XpLedgerEntry | Id, PlanLogId, AreaId, Amount, Reason, CreatedAt | Sổ cái chỉ thêm, không sửa (BR-07) |
| WeeklyReview | Id, WeekStart, Score, PlansDone, PlansTotal, Consistency, XpGained, Note, CompletedAt? | Snapshot JSON số liệu theo Area |
| Recommendation | Id, ReviewId?, PlanId, Kind, CurrentValue, SuggestedValue, Reason, State, SnoozedUntil? | Kind: DecreaseTarget, IncreaseTarget |
| AppSetting | Key, Value | Theme, giờ tạo review, ẩn XP |

Mọi bảng (trừ PlanGoalLink, AppSetting) có thêm: CreatedAt, UpdatedAt, IsDeleted. Khoá chính là GUID v7 lưu dạng TEXT.

### 4.2. Index

| Index | Phục vụ |
| --- | --- |
| PlanOccurrence(ScheduledDate, State) | Today, Lịch biểu tháng/tuần |
| PlanOccurrence(PlanId, ScheduledDate) | Consistency 7 ngày, Risk 14 ngày |
| PlanLog(LoggedAt) | Xu hướng Score, Weekly Review |
| XpLedgerEntry(AreaId) | Tổng XP, Level theo Area |
| Goal(AreaId, Status) | Danh sách Goals, lọc |

### 4.3. Cấu hình SQLite

Bật `PRAGMA journal_mode=WAL` và `foreign_keys=ON`; file dữ liệu đặt tại `%LOCALAPPDATA%\GrowthTracker\growth.db` trên Windows và `$XDG_DATA_HOME/growth-tracker/growth.db` (mặc định `~/.local/share/...`) trên Linux. Migration chạy tự động khi khởi động, sau khi đã tạo bản sao lưu.

## 5. Domain services

6 bộ tính là hàm thuần trong GrowthTracker.Domain, có unit test cho mọi ví dụ trong BRD. Trọng số và ngưỡng đặt trong cấu hình có phiên bản, vì BRD còn đang chốt vấn đề Difficulty bị tính hai lần (BRD mục 9.3).

### 5.1. Score Engine

```csharp
public sealed record ScoreWeights(decimal Completion, decimal Quality,
                                  decimal Consistency, decimal Difficulty)
{
    // v1 = BRD gốc: 40 / 25 / 20 / 15
    public static readonly ScoreWeights V1 = new(0.40m, 0.25m, 0.20m, 0.15m);
    // v2 = đề xuất trong BRD: bỏ Difficulty khỏi Score
    public static readonly ScoreWeights V2 = new(0.45m, 0.30m, 0.25m, 0m);
}

public sealed class ScoreEngine(ScoreWeights weights, DifficultyTable difficulty)
{
    public ScoreBreakdown Calculate(ScoreInput input)
    {
        decimal c = Completion(input);                         // 0..100, BR-09
        decimal q = input.Quality / 5m * 100m;                 // 1..5 sao
        decimal s = Math.Min(100m, input.SessionsDoneLast7Days
                              / (decimal)input.SessionsTargetLast7Days * 100m);
        decimal d = difficulty.Points(input.Difficulty);       // Easy 60 ... Extreme 100

        decimal raw = c * weights.Completion + q * weights.Quality
                    + s * weights.Consistency + d * weights.Difficulty;
        int score = (int)Math.Round(Math.Min(100m, raw),       // BR-05
                                    MidpointRounding.AwayFromZero);
        decimal xp = score * difficulty.XpMultiplier(input.Difficulty); // BR-06
        return new ScoreBreakdown(c, q, s, d, score, xp);
    }

    private static decimal Completion(ScoreInput i) => i.PlanType switch
    {
        PlanType.Time or PlanType.Quantity =>
            Math.Min(1m, i.Actual / i.Target) * 100m,
        PlanType.Binary or PlanType.Frequency => i.Done ? 100m : 0m,
        PlanType.Achievement => i.CriteriaDone / (decimal)i.CriteriaTotal * 100m,
        _ => throw new ArgumentOutOfRangeException()
    };
}
```

Test bắt buộc: C = 90, Q = 80, S = 100, D = 90 với V1 phải cho Score 90 (89,5 làm tròn lên) và XP 103,5 khi Hard.

### 5.2. Các bộ tính khác

| Service | Input | Output | Quy tắc |
| --- | --- | --- | --- |
| XpCalculator / LevelTable | Tổng XP theo Area | Level, XP còn thiếu lên Level kế tiếp | Ngưỡng Level đặt trong bảng cấu hình, chờ BRD chốt công thức |
| StreakCalculator | Danh sách ngày có PlanLog Done | Current, Longest | Ngày có ít nhất một Done; ngày đánh dấu nghỉ không làm đứt chuỗi |
| ProgressCalculator | Plan Log, Occurrence, trọng số | Progress Plan → Milestone → Goal → Area | BRD mục 5.5; ExpectedProgress = ngày đã qua ÷ tổng ngày |
| RiskDetector | Goal, Plan, 14 ngày dữ liệu | Status At Risk, cảnh báo Plan | Goal: thực tế < kỳ vọng − 10 điểm %; Plan: Consistency < 50% trong 14 ngày |
| Recommender | Completion và Consistency 4 tuần | Recommendation | < 60% → giảm target; ≥ 85% → tăng target; không gợi ý lại trong 4 tuần sau khi bỏ qua |
| ScheduleGenerator | Plan (Frequency, DaysOfWeek, Duration) | Danh sách PlanOccurrence | Sinh trước 8 tuần, sinh tiếp hằng tuần; không sinh vào ngày đã qua (FR-24) |

### 5.3. Thời gian và múi giờ

Mọi "ngày" nghiệp vụ là `DateOnly` theo múi giờ của UserProfile; thời điểm lưu ở UTC. IClock được giả lập trong test để kiểm tra chuyển ngày, chuyển tuần và quy tắc sửa trong 24 giờ (BR-11).

## 6. Tầng UI (Avalonia)

UI dùng MVVM với CommunityToolkit.Mvvm; mỗi màn hình là một cặp View (AXAML) + ViewModel, ViewModel chỉ gọi Use Case của tầng Application, không chạm DbContext.

### 6.1. Ánh xạ 7 màn hình

| Màn hình | View / ViewModel | Query / Command chính | Control đặc biệt |
| --- | --- | --- | --- |
| Dashboard | DashboardView / DashboardViewModel | GetDashboardSummary, GetScoreTrend | ScoreRing, AreaCard, biểu đồ đường |
| Today | TodayView / TodayViewModel | GetTodayOccurrences, SkipOccurrence | PlanItem, công tắc Binary |
| Plan Result | PlanResultDialog / PlanResultViewModel | LogPlanResult, EditPlanLog | StarRating, bộ nhập theo PlanType, bấm giờ |
| Lịch biểu | CalendarView / CalendarViewModel | GetMonthView, GetWeekView, ReschedulePlan | MonthGrid, WeekTimeline, kéo thả |
| Goals | GoalsView / GoalsViewModel | GetGoals (lọc theo Area, Status) | GoalCard, ProgressBar có vạch kỳ vọng |
| Goal Detail | GoalDetailView / GoalDetailViewModel | GetGoalDetail, AddMilestone, CreatePlan | Cây Milestone → Plan, RiskBanner |
| Weekly Review | WeeklyReviewView / WeeklyReviewViewModel | GetWeeklyReview, ApplyRecommendation | Bảng Area, thẻ gợi ý |

### 6.2. Khung ứng dụng

| Thành phần | Cách làm |
| --- | --- |
| Cửa sổ chính | MainWindow: sidebar 240px (thu gọn 72px) + vùng nội dung ContentControl gắn CurrentPage |
| Điều hướng | NavigationService giữ ngăn xếp ViewModel; ViewLocator ánh xạ ViewModel → View theo tên |
| Hộp thoại | DialogService mở Plan Result dạng overlay bên phải, trả kết quả qua Task |
| Theme | Fluent theme; token màu (mục 2 của UI Design) đặt trong ResourceDictionary Light / Dark; theo theme hệ điều hành mặc định |
| Font | Inter nhúng trong Assets để hiển thị giống nhau trên Windows và Linux |
| Phím tắt | KeyBinding toàn cục: Ctrl+N thêm Plan, Ctrl+L ghi nhận, T về hôm nay trong Lịch |
| Khay hệ thống | TrayIcon: mở nhanh Today, ghi nhận nhanh, thoát |

### 6.3. Control tự xây

| Control | Cách cài đặt |
| --- | --- |
| ScoreRing | Control tự vẽ (override Render) bằng Arc; hoạt ảnh 600ms qua Animation |
| MonthGrid | UniformGrid 7 cột × 6 hàng; mỗi ô là DayCell với ItemsControl giới hạn 3 chip + "+N khác" |
| WeekTimeline | Canvas theo giờ; vị trí khối tính từ ScheduledTime và thời lượng |
| Kéo thả | DragDrop của Avalonia: chip là nguồn kéo, DayCell là đích; thả xong gọi ReschedulePlan và hỏi "Chỉ lần này / Các lần sau" |
| StarRating | ItemsControl 5 ToggleButton, điều khiển được bằng phím mũi tên |

### 6.4. Hiệu năng UI

Query của từng màn hình chạy bất đồng bộ, trả DTO đã tính sẵn; danh sách dài (Lịch sử Plan Log) dùng VirtualizingStackPanel. Mục tiêu: chuyển màn hình dưới 150ms, mở tháng lịch dưới 200ms với 300 buổi.

## 7. Tác vụ nền, thông báo, lưu trữ và đăng nhập

### 7.1. Tác vụ nền

Desktop không có server, nên mọi tác vụ định kỳ chạy trong tiến trình app bằng BackgroundService của Generic Host. Khi app khởi động, job **bù lại** những lần chạy bị lỡ lúc app tắt.

| Job | Lịch chạy | Việc làm |
| --- | --- | --- |
| OccurrenceRollover | Mỗi lần mở app và lúc 00:05 | Đổi buổi đã qua chưa ghi nhận sang Missed; sinh buổi mới cho 8 tuần tới |
| WeeklyReviewJob | Chủ nhật 20:00 (hoặc lần mở app đầu tiên sau đó) | Tạo WeeklyReview và Recommendation của tuần |
| RiskScanJob | Mỗi ngày 06:00 | Chạy RiskDetector cho mọi Goal và Plan đang Active |
| ReminderJob | Theo PreferredTime của Plan | Gửi thông báo trước 10 phút |
| BackupJob | Mỗi ngày, lần mở app đầu tiên | Sao lưu file SQLite |

### 7.2. Thông báo

| Hệ điều hành | Cách gửi | Ghi chú |
| --- | --- | --- |
| Windows 10/11 | Toast notification qua Windows App SDK / WinRT | Cần AppUserModelID; Velopack tạo shortcut kèm ID |
| Linux | Giao thức org.freedesktop.Notifications qua D-Bus (thư viện Tmds.DBus) | Chạy trên GNOME, KDE, XFCE; nếu không có D-Bus thì chỉ hiện trong app |

INotifier là interface ở Application; Infrastructure chọn cài đặt theo `OperatingSystem.IsWindows()` / `IsLinux()`. App có tuỳ chọn chạy nền ở khay hệ thống và khởi động cùng hệ điều hành để thông báo hoạt động.

### 7.3. Lưu trữ và sao lưu

| Hạng mục | Windows | Linux |
| --- | --- | --- |
| Dữ liệu | %LOCALAPPDATA%\\GrowthTracker\\growth.db | \~/.local/share/growth-tracker/growth.db |
| Sao lưu | %LOCALAPPDATA%\\GrowthTracker\\backups\\ | \~/.local/share/growth-tracker/backups/ |
| Log | %LOCALAPPDATA%\\GrowthTracker\\logs\\ | \~/.local/state/growth-tracker/logs/ |
| Cấu hình | %APPDATA%\\GrowthTracker\\settings.json | \~/.config/growth-tracker/settings.json |

Sao lưu dùng SQLite Online Backup API (an toàn khi đang ghi), giữ 7 bản hằng ngày + 4 bản hằng tuần. Người dùng xuất / nhập dữ liệu dạng file .db hoặc JSON trong Settings.

### 7.4. Đăng nhập

BRD yêu cầu Authentication, nhưng app desktop local-first chỉ có một người dùng. MVP dùng **hồ sơ cục bộ**: tạo tên hiển thị lần đầu, tuỳ chọn đặt mã PIN khoá app (băm bằng PBKDF2, lưu trong UserProfile). Tài khoản online chỉ cần khi thêm đồng bộ cloud ở giai đoạn sau.

## 8. Build, đóng gói và phân phối

Mỗi bản phát hành build **self-contained** cho từng hệ điều hành (người dùng không cần cài .NET), rồi Velopack đóng gói thành bộ cài và gói cập nhật delta.

### 8.1. Đầu ra theo nền tảng

| Nền tảng | Runtime ID | Đầu ra Velopack | Cài đặt |
| --- | --- | --- | --- |
| Windows x64 | win-x64 | Setup.exe + gói Portable | Một chạm, cài vào %LocalAppData%, không cần quyền admin |
| Windows ARM64 | win-arm64 | Setup.exe | Như trên |
| Linux x64 | linux-x64 | AppImage | Tải về, cấp quyền chạy, mở; không cần root |

Gói .deb / .rpm / Flatpak có thể bổ sung sau nếu cần đưa lên kho phần mềm của distro.

### 8.2. Lệnh build

```bash
# Windows
dotnet publish src/GrowthTracker.Desktop -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=false -o publish/win-x64
vpk pack --packId GrowthTracker --packVersion 1.0.0 \
  --packDir publish/win-x64 --mainExe GrowthTracker.Desktop.exe

# Linux
dotnet publish src/GrowthTracker.Desktop -c Release -r linux-x64 \
  --self-contained -o publish/linux-x64
vpk pack --packId GrowthTracker --packVersion 1.0.0 \
  --packDir publish/linux-x64 --mainExe GrowthTracker.Desktop
```

Bật ReadyToRun để rút ngắn thời gian khởi động; chưa dùng Native AOT vì EF Core và reflection của Avalonia cần kiểm chứng thêm.

### 8.3. Cập nhật tự động

Program.Main gọi `VelopackApp.Build().Run()` trước khi khởi động Avalonia. Khi mở app, UpdateService kiểm tra GitHub Releases ở nền; có bản mới thì tải gói delta, hiện thông báo "Có bản cập nhật — khởi động lại". Kênh stable và beta tách riêng.

### 8.4. CI/CD (GitHub Actions)

| Bước | Runner | Kết quả |
| --- | --- | --- |
| Build + test | windows-latest, ubuntu-latest (matrix) | Chạy toàn bộ test, báo cáo coverage |
| Đóng gói | Mỗi OS trên runner tương ứng | Setup.exe, AppImage, gói delta |
| Ký số | windows-latest | Ký Authenticode cho Setup.exe để giảm cảnh báo SmartScreen |
| Phát hành | ubuntu-latest | Đẩy lên GitHub Releases khi tạo tag v\*.*.* |

**Ma trận kiểm thử thủ công trước mỗi bản:** Windows 10 22H2, Windows 11, Ubuntu 24.04 (GNOME, Wayland), Fedora (KDE), một máy màn hình HiDPI 150–200%.

## 9. Kiểm thử, logging, bảo mật và hiệu năng

### 9.1. Kiểm thử

| Tầng | Công cụ | Phạm vi | Mục tiêu coverage |
| --- | --- | --- | --- |
| Domain | xUnit, FluentAssertions | Score Engine (mọi PlanType, mọi Difficulty, làm tròn, trần 100), Progress, Risk, Recommender, Streak, ScheduleGenerator | ≥ 90% |
| Application | xUnit + SQLite in-memory | Use Case end-to-end: ghi nhận → XP → Progress → Risk trong một transaction | ≥ 80% |
| Infrastructure | xUnit | Migration, sao lưu / khôi phục, đường dẫn theo OS | Các luồng chính |
| UI | Avalonia.Headless | ViewModel và các control tự xây (MonthGrid, StarRating, kéo thả) | Luồng hằng ngày |

Các ví dụ trong BRD được viết thành test có tên rõ ràng, ví dụ `Score_BrdExample_Returns90`, `Gym_48PercentIn4Weeks_SuggestsDecreaseTo3`.

### 9.2. Logging và chẩn đoán

- Serilog ghi file xoay vòng theo ngày, giữ 14 ngày, mức Information mặc định.
- Không ghi nội dung Note hay tên Goal vào log, chỉ ghi Id.
- Bắt lỗi toàn cục (AppDomain, TaskScheduler, Dispatcher) để ghi log và hiện hộp thoại "Đã xảy ra lỗi" kèm nút mở thư mục log.

### 9.3. Bảo mật và quyền riêng tư

| Vấn đề | Cách xử lý |
| --- | --- |
| Dữ liệu cá nhân | Không gửi dữ liệu ra ngoài; app không cần mạng trừ khi kiểm tra cập nhật |
| Khoá app | Mã PIN tuỳ chọn, băm PBKDF2 + salt |
| Mã hoá file dữ liệu | Không mã hoá trong MVP; tuỳ chọn SQLCipher ở giai đoạn sau |
| Cập nhật | Chỉ tải từ GitHub Releases qua HTTPS; bộ cài Windows được ký số |
| Telemetry | Mặc định tắt; nếu thêm sau phải hỏi người dùng |

### 9.4. Mục tiêu hiệu năng

| Chỉ số | Mục tiêu |
| --- | --- |
| Khởi động nguội đến màn hình Today | Dưới 2 giây trên máy SSD |
| Lưu Plan Result và hiện Score | Dưới 100ms |
| RAM khi chạy | Dưới 200 MB |
| Dung lượng dữ liệu 2 năm (10 Plan/ngày) | Khoảng 7.300 Plan Log, dưới 20 MB |

## 10. Kế hoạch triển khai MVP và câu hỏi mở

Ước tính 6 sprint × 2 tuần (khoảng 12 tuần) cho một developer làm bán thời gian đều đặn; mỗi sprint kết thúc bằng một bản chạy được trên cả Windows và Linux.

| Sprint | Nội dung | Kết quả kiểm chứng |
| --- | --- | --- |
| 1. Nền móng | Solution, DI/Host, EF Core + SQLite, migration đầu, khung MainWindow + sidebar + theme, CI build 2 OS | App trống mở được trên Windows và Linux từ CI |
| 2. Domain | Score Engine, XP/Level, Progress, Streak, ScheduleGenerator + toàn bộ unit test theo ví dụ BRD | Test Domain xanh, coverage ≥ 90% |
| 3. Mục tiêu | Area, Goal, Milestone, Plan (5 loại); màn hình Goals, Goal Detail | Tạo được Goal → Milestone → Plan và thấy buổi được sinh ra |
| 4. Hằng ngày | Today, Plan Result, bỏ qua buổi, sửa trong 24 giờ, thông báo nhắc | Ghi nhận một Plan dưới 60 giây, Score và XP hiện đúng |
| 5. Lịch và phân tích | Lịch biểu Tháng / Tuần / Danh sách, kéo thả dời lịch; Dashboard, biểu đồ xu hướng | Dời lịch cập nhật Reschedule Rate; Dashboard khớp dữ liệu |
| 6. Review và phát hành | Weekly Review, RiskDetector, Recommender, sao lưu, Velopack + auto-update, ký số | Bản 1.0.0 trên GitHub Releases cho Windows và Linux |

### 10.1. Câu hỏi kỹ thuật mở

- [ ] Chọn trọng số Score V1 (BRD gốc) hay V2 (bỏ Difficulty khỏi Score) làm mặc định khi phát hành?
- [ ] Có cần macOS trong bản 1.0 không? Kiến trúc đã hỗ trợ, nhưng cần máy Mac để build và ký.
- [ ] Có mua chứng chỉ ký số Windows (Authenticode) ngay từ bản đầu không?
- [ ] Ngoài AppImage, có cần .deb hoặc Flatpak cho Linux ở bản 1.0 không?
- [ ] Có cần đồng bộ nhiều máy (ví dụ một máy Windows, một máy Linux) trong năm đầu không? Nếu có, nên chọn sớm giữa tự dựng API ASP.NET Core hay đồng bộ file qua thư mục cloud.
- [ ] Có bật mã hoá file dữ liệu (SQLCipher) mặc định không?

### 10.2. Tài liệu liên quan

- BRD — Personal Growth Tracker v1.0
- UI Design Document — Personal Growth Tracker v1.0
