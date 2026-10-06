# UI Design Document — Personal Growth Tracker v1.0

Oct 6, 2026 · @Alex

## 1. Tổng quan & nguyên tắc thiết kế

UI của Personal Growth Tracker phải trả lời được một câu hỏi trong 3 giây đầu mở app: **"Hôm nay tôi cần làm gì, và tôi đang tiến bộ đến đâu?"** Tài liệu này mô tả giao diện cho 7 màn hình MVP: 6 màn hình trong BRD v1.0 (Dashboard, Goals, Goal Detail, Today, Plan Result, Weekly Review) và màn hình Lịch biểu.

**Người dùng chính:** cá nhân tự quản lý phát triển bản thân (developer, sinh viên, freelancer), dùng app mỗi ngày 2 lần: sáng xem kế hoạch (dưới 30 giây), tối ghi nhận kết quả (dưới 60 giây cho mỗi Plan).

**5 nguyên tắc thiết kế**

1. **Hành động trước, phân tích sau.** Màn hình mở đầu là Today hoặc Dashboard có khối Today ở trên cùng; biểu đồ nằm bên dưới.
2. **Score là kết quả, không phải ô nhập.** Người dùng nhập Actual, Quality và Note; Score hiện ra ngay sau khi lưu (Business Rule 3).
3. **Một màu, một nghĩa.** Mỗi Area có một màu cố định; mỗi trạng thái (On Track, At Risk...) có một màu cố định trên toàn app.
4. **Xu hướng quan trọng hơn con số.** Mọi Score đi kèm mũi tên ↑ → ↓ và so sánh với kỳ trước.
5. **Cảnh báo phải kèm lối thoát.** Khi hiện "At Risk", luôn có nút hành động (Giảm target, Dời deadline, Xem kế hoạch).

**Nền tảng:** Web responsive làm trước (desktop 1280px, mobile 390px). Thiết kế mobile-first cho Today và Plan Result vì đây là hai màn hình dùng hằng ngày.

## 2. Design System

Bộ token dưới đây áp dụng cho mọi màn hình; giá trị cụ thể có thể chỉnh khi làm prototype.

### 2.1. Màu theo Area

Mỗi Area giữ một màu nhận diện cố định ở thanh tiến độ, huy hiệu và biểu đồ.

| Area | Màu | Hex | Dùng cho |
| --- | --- | --- | --- |
| Career | Xanh dương | #2563EB | Thanh tiến độ, icon, viền thẻ |
| English | Tím | #7C3AED | Như trên |
| Health | Xanh lá | #16A34A | Như trên |
| Finance | Cam | #EA580C | Như trên |
| Knowledge | Xanh ngọc | #0D9488 | Như trên |
| Personal | Hồng | #DB2777 | Như trên |

### 2.2. Màu theo trạng thái

Trạng thái luôn có **màu + icon + chữ**, không chỉ dựa vào màu (hỗ trợ người mù màu).

| Trạng thái | Màu | Icon | Áp dụng |
| --- | --- | --- | --- |
| Draft | Xám #6B7280 | ○ | Goal |
| Active | Xanh dương #2563EB | ● | Goal |
| On Track | Xanh lá #16A34A | ✓ | Goal |
| At Risk | Vàng cam #D97706 | ⚠ | Goal, Plan |
| Completed | Xanh đậm #15803D | ★ | Goal, Milestone |
| Cancelled | Xám nhạt #9CA3AF | ✕ | Goal |

### 2.3. Màu theo Score

| Khoảng Score | Nhãn | Màu |
| --- | --- | --- |
| 90–100 | Xuất sắc | Xanh đậm #15803D |
| 75–89 | Tốt | Xanh lá #16A34A |
| 60–74 | Đạt | Vàng #CA8A04 |
| 40–59 | Cần cải thiện | Cam #EA580C |
| 0–39 | Yếu | Đỏ #DC2626 |

### 2.4. Typography, spacing, hình khối

| Thành phần | Quy định |
| --- | --- |
| Font | Inter (fallback: system-ui, sans-serif) |
| Số lớn (Score, Level) | 48 / 32px, đậm 700, chữ số đều nhau (tabular) |
| Tiêu đề màn hình | 24px, 600 |
| Tiêu đề thẻ | 16px, 600 |
| Nội dung | 14px, 400, dòng cao 1.5 |
| Chú thích | 12px, màu phụ |
| Lưới spacing | Bội số của 4px (4, 8, 12, 16, 24, 32) |
| Bo góc | Thẻ 12px, nút và ô nhập 8px, huy hiệu 999px |
| Độ cao thẻ | Viền 1px, không đổ bóng nặng |
| Icon | Bộ nét mảnh 1.5px, kích thước 20px |

### 2.5. Chế độ sáng / tối

Định nghĩa màu bằng token (nền, thẻ, chữ chính, chữ phụ, viền) và có bản light lẫn dark ngay từ đầu. Màu Area và trạng thái giữ nguyên hue, chỉ tăng độ sáng 10–15% ở dark mode để đạt tương phản.

## 3. Sitemap & điều hướng

App có 6 mục điều hướng chính; 7 màn hình MVP nằm trong đó.

```text
Personal Growth Tracker
├── Dashboard            (màn hình 1)
├── Today                (màn hình 4)
│     └── Plan Result    (màn hình 5, mở dạng bảng trượt)
├── Lịch biểu            (màn hình 7)
│     ├── Tháng / Tuần / Danh sách
│     └── Panel ngày đã chọn
├── Goals                (màn hình 2)
│     └── Goal Detail    (màn hình 3)
│           ├── Milestones
│           └── Plans
├── Review
│     └── Weekly Review  (màn hình 6)
└── Settings (Area, tài khoản, giao diện)
```

### 3.1. Điều hướng theo thiết bị

| Thiết bị | Cách điều hướng | Ghi chú |
| --- | --- | --- |
| Desktop (từ 1024px) | Thanh bên trái, rộng 240px, thu gọn được còn 72px | Logo, 5 mục chính, Settings ở cuối |
| Tablet (768–1023px) | Thanh bên thu gọn (chỉ icon) | Nhấn vào để mở rộng |
| Mobile (dưới 768px) | Thanh tab dưới cùng, 5 mục: Dashboard, Today, Lịch, Goals, Review | Nút **+ Ghi nhận** nổi ở giữa; Settings nằm trong ảnh đại diện |

### 3.2. Luồng người dùng chính

1. **Luồng hằng ngày:** Mở app → Today → chọn Plan → Plan Result (nhập Actual, Quality, Note) → thấy Score + XP → quay lại Today.
2. **Luồng cuối tuần:** Thông báo Weekly Review → xem tổng quan → bấm vào Area yếu → Goal Detail → điều chỉnh Plan.
3. **Luồng tạo mục tiêu:** Goals → Tạo Goal → thêm Milestone → thêm Plan (chọn loại đo lường) → Plan xuất hiện trong Today theo lịch.

### 3.3. Mặc định khi mở app

Buổi sáng đến trưa (trước 17:00) mở **Today**; từ 17:00 mở **Today ở chế độ ghi nhận**; ngày đầu tuần hiện thêm banner Weekly Review nếu chưa xem.

## 4. Component library

8 thành phần dùng lặp lại ở nhiều màn hình. Thiết kế một lần, tái sử dụng khắp app.

| Component | Mô tả | Biến thể / trạng thái | Dùng ở |
| --- | --- | --- | --- |
| **Score Ring** | Vòng tròn 0–100, số ở giữa, màu theo khoảng Score (mục 2.3) | Lớn 120px, vừa 64px, nhỏ 32px; có/không có mũi tên xu hướng | Dashboard, Plan Result, Weekly Review |
| **Progress Bar** | Thanh ngang, màu theo Area, hiện % ở cuối | Có mốc "Kỳ vọng" (vạch đứng) để thấy chậm hay nhanh hơn kế hoạch | Area Card, Goal Card, Milestone |
| **Area Card** | Tên Area, Score, mũi tên xu hướng, Progress Bar, Level | Mặc định, hover, đang chọn, At Risk (viền cam) | Dashboard |
| **Plan Item** | Hàng một Plan: ô tick, tên, target, loại đo lường, trạng thái | Chưa làm, đang làm, đã ghi (kèm Score), bỏ qua | Today, Goal Detail |
| **Star Rating** | 5 sao nhấn được, nhãn chữ bên dưới (1 = Kém ... 5 = Rất tốt) | Mặc định, đã chọn, chỉ đọc | Plan Result |
| **Trend Chip** | Mũi tên + chênh lệch so với kỳ trước | ↑ xanh, → xám, ↓ đỏ | Mọi nơi có Score |
| **Status Badge** | Màu + icon + chữ theo bảng trạng thái (mục 2.2) | 6 trạng thái | Goal Card, Goal Detail |
| **Risk Banner** | Dải cảnh báo: nguyên nhân + 1–2 nút hành động | Cảnh báo (cam), nghiêm trọng (đỏ) | Goal Detail, Weekly Review |

### 4.1. Ô nhập theo loại Plan

Plan Result đổi ô nhập chính theo loại Plan (BRD mục 10).

| Loại Plan | Ô nhập chính | Ví dụ giao diện |
| --- | --- | --- |
| Time-based | Bộ đếm phút (− 5 / + 5) hoặc nút Bấm giờ | "25 phút" |
| Quantity-based | Ô số có nút − / + | "8 / 10 từ" |
| Frequency-based | Chấm tròn theo ngày trong tuần, nhấn để đánh dấu | ●●●○○ 3/5 buổi |
| Binary | Công tắc Có / Không | "Đã thiền hôm nay" |
| Achievement-based | Danh sách tiêu chí con để tick | ☑ Nộp hồ sơ ☐ Làm bài thi |

### 4.2. Nguyên tắc cho form

- Chỉ có **3 trường** bắt buộc khi ghi nhận: Actual, Quality, Note (Note có thể bỏ trống).
- Giá trị Actual mặc định bằng Target để người dùng chỉ cần chỉnh khi khác.
- Không có ô nhập Score; chỉ có vùng hiển thị Score sau khi lưu.

## 5. Màn hình 1: Dashboard

**Mục đích:** cho người dùng thấy tổng thể tiến độ phát triển và việc cần làm hôm nay trên một màn hình, không cần cuộn trên desktop.

### 5.1. Wireframe (desktop)

```text
┌──────────┬───────────────────────────────────────────────────────┐
│ Sidebar  │  Chào buổi sáng, [Tên]            [Tuần ▾]  [+ Ghi nhận]│
│          ├───────────────────────────────────────────────────────┤
│ Dashboard│  ┌─────────────┐  ┌───────────────────────────────┐   │
│ Today    │  │  (72)       │  │ Xu hướng Score 6 tuần          │   │
│ Goals    │  │ Overall     │  │      ╭──╮                      │   │
│ Review   │  │ Level 12    │  │  ╭───╯  ╰─╮                    │   │
│          │  │ 8,420 XP    │  │ W1 W2 W3 W4 W5 W6              │   │
│          │  │ Streak 14🔥 │  └───────────────────────────────┘   │
│          │  └─────────────┘                                       │
│          ├───────────────────────────────────────────────────────┤
│          │  THEO LĨNH VỰC                                         │
│          │  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐          │
│          │  │Career  │ │English │ │Health  │ │Finance │          │
│          │  │78 ↑    │ │65 ↑    │ │84 →    │ │51 ↓ ⚠  │          │
│          │  │███░ Lv8│ │██░░ Lv5│ │███░ Lv7│ │█░░░ Lv3│          │
│          │  └────────┘ └────────┘ └────────┘ └────────┘          │
│          ├───────────────────────────────────────────────────────┤
│          │  HÔM NAY (2/4)                         [Xem tất cả →] │
│          │  ☑ English     ☑ Gym     ☐ System Design   ☐ Đọc sách │
└──────────┴───────────────────────────────────────────────────────┘
```

### 5.2. Thành phần

| Khối | Nội dung | Hành vi |
| --- | --- | --- |
| Header | Lời chào, bộ lọc kỳ (Tuần / Tháng / Quý), nút **+ Ghi nhận** | Đổi kỳ thì mọi số liệu trên trang đổi theo |
| Overall | Score Ring lớn, Level, tổng XP, Streak hiện tại | Nhấn vào mở bảng chi tiết: Longest Streak, XP theo Area |
| Xu hướng | Biểu đồ đường Score 6 tuần, nhãn "Đang cải thiện / Ổn định / Đang giảm" | Rê chuột vào điểm để xem Score và số Plan của tuần đó |
| Area Cards | 1 thẻ mỗi Area đang dùng (tối đa 6), sắp xếp theo thứ tự người dùng chọn | Nhấn vào mở danh sách Goal của Area đó |
| Hôm nay | Tối đa 4 Plan, phần còn lại "+ 3 Plan khác" | Tick nhanh với Plan dạng Binary; Plan khác mở Plan Result |
| Cảnh báo | Dải Risk Banner nếu có Goal At Risk (hiện dưới header) | Nút "Xem Goal", "Bỏ qua 7 ngày" |

### 5.3. Responsive

- **Mobile:** xếp 1 cột theo thứ tự Hôm nay → Overall → Area Cards (cuộn ngang) → Xu hướng.
- **Tablet:** Area Cards 2 cột.

### 5.4. Trạng thái

| Trạng thái | Hiển thị |
| --- | --- |
| Người dùng mới | Không có số; hiện 3 bước: tạo Area → tạo Goal → tạo Plan đầu tiên |
| Chưa đủ dữ liệu (dưới 7 ngày) | Score hiện "Đang thu thập dữ liệu", ẩn biểu đồ xu hướng |
| Đang tải | Khung xám (skeleton) đúng kích thước từng khối |
| Lỗi tải | Thẻ lỗi từng khối kèm nút Thử lại; khối khác vẫn hoạt động |

## 6. Màn hình 2 & 3: Goals và Goal Detail

### 6.1. Goals (danh sách)

**Mục đích:** xem toàn bộ mục tiêu, lọc theo Area / trạng thái, và phát hiện Goal đang gặp rủi ro.

```text
┌───────────────────────────────────────────────────────────────┐
│ Goals                                          [+ Tạo Goal]   │
│ [Tất cả] [Career] [English] [Health] [Finance]   Trạng thái ▾ │
├───────────────────────────────────────────────────────────────┤
│ ┌───────────────────────────────────────────────────────────┐ │
│ │ ● Career   Become Senior .NET Developer      [On Track ✓] │ │
│ │ 01/10/2026 → 01/10/2028                                   │ │
│ │ ████████░░░░░░░░░░░░  38%   (kỳ vọng 35%)      Score 78 ↑ │ │
│ └───────────────────────────────────────────────────────────┘ │
│ ┌───────────────────────────────────────────────────────────┐ │
│ │ ● English  Improve English                    [At Risk ⚠] │ │
│ │ 01/01/2027 → 30/06/2027                                   │ │
│ │ ██████░░░░░░░░░░░░░░  38%   (kỳ vọng 55%)      Score 65 ↑ │ │
│ └───────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────┘
```

| Thành phần | Quy định |
| --- | --- |
| Goal Card | Dải màu Area bên trái, tên, ngày bắt đầu → mục tiêu, Status Badge, Progress Bar có vạch "kỳ vọng", Score, Trend Chip |
| Bộ lọc | Tab theo Area; menu trạng thái chọn nhiều giá trị; mặc định ẩn Cancelled |
| Sắp xếp | Mặc định: At Risk lên đầu, sau đó theo Priority, rồi theo deadline gần nhất |
| Tạo Goal | Hộp thoại 2 bước: (1) Tên, Area, mô tả, ngày; (2) Priority, Target đo lường |
| Rỗng | Hình minh họa + câu "Bạn muốn phát triển điều gì trong 6 tháng tới?" + nút Tạo Goal |

### 6.2. Goal Detail

**Mục đích:** hiểu một Goal đang ở đâu (Milestone, Plan, xu hướng) và điều chỉnh kế hoạch.

```text
┌───────────────────────────────────────────────────────────────┐
│ ← Goals                                      [Sửa] [⋯]        │
│ Improve English                         [At Risk ⚠]           │
│ English · Priority: High · 01/01/2027 → 30/06/2027            │
├───────────────────────────────────────────────────────────────┤
│ ⚠ Tiến độ 38%, thấp hơn kỳ vọng 55%.                          │
│   [Giảm target Plan]  [Dời deadline]  [Xem gợi ý]             │
├────────────────────┬──────────────────────────────────────────┤
│  (65)  Score       │  Tiến độ theo thời gian                   │
│  Tiến độ 38%       │  ── Thực tế   ┄┄ Kỳ vọng                 │
│  Còn 142 ngày      │                                           │
├────────────────────┴──────────────────────────────────────────┤
│ [Milestones] [Plans] [Lịch sử]                                │
│                                                               │
│ ☑ 1. Nền tảng từ vựng (100%)                                  │
│ ▸ 2. Nghe hiểu (60%)                                          │
│     • Listening 20 phút/ngày        Score TB 82 ↑  5/7 ngày   │
│     • Shadowing 3 lần/tuần          Score TB 54 ↓  1/3 buổi ⚠ │
│ ○ 3. Giao tiếp (0%)                                           │
└───────────────────────────────────────────────────────────────┘
```

| Khối | Nội dung | Hành vi |
| --- | --- | --- |
| Header | Tên, Area, Priority, ngày, Status Badge | "Sửa" mở hộp thoại; "⋯" gồm Hủy Goal, Nhân bản |
| Risk Banner | Lý do (tiến độ thực tế so với kỳ vọng) + 2–3 hành động | Chỉ hiện khi trạng thái là At Risk; nút "Xem gợi ý" mở bảng đề xuất (BRD mục 21) |
| Tổng quan | Score Ring, % tiến độ, số ngày còn lại | Không nhập tay (Business Rule 8: tiến độ không chỉ đếm số task) |
| Biểu đồ tiến độ | Hai đường: thực tế và kỳ vọng | Khoảng cách giữa hai đường tô nhẹ để thấy chênh lệch |
| Tab Milestones | Cây Milestone → Plan; mở/thu gọn từng nhóm | Mỗi Plan hiện Score trung bình, Trend Chip, tỷ lệ đạt tần suất |
| Tab Plans | Danh sách phẳng mọi Plan của Goal, lọc được | Nút **+ Thêm Plan** (Business Rule 2: bắt buộc chọn cách đo lường) |
| Tab Lịch sử | Nhật ký Plan Log theo ngày, mới nhất trước | Nhấn vào một dòng để xem hoặc sửa bản ghi |

**Plan có rủi ro:** Plan có consistency dưới 50% trong 14 ngày hiện nhãn ⚠ "Có thể quá khó" ngay trên hàng Plan, kèm nút "Giảm target".

## 7. Màn hình 4 & 5: Today và Plan Result

Đây là hai màn hình dùng hằng ngày, thiết kế **mobile-first**.

### 7.1. Today

**Mục đích:** liệt kê Plan của hôm nay và cho phép ghi nhận kết quả nhanh.

```text
┌─────────────────────────────┐
│ Thứ Ba, 06/10      Streak 14🔥│
│ Hôm nay: 2/4 hoàn thành      │
│ ████████░░░░░░  50%          │
├─────────────────────────────┤
│ SÁNG                         │
│ ☑ English Speaking    Score 92│
│   25/30 phút  ★★★★☆          │
│ ☑ Gym                 Score 85│
│                              │
│ TỐI                          │
│ ☐ System Design        [Ghi ›]│
│   Mục tiêu 45 phút           │
│ ☐ Đọc 15 trang         [Ghi ›]│
├─────────────────────────────┤
│ ĐÃ BỎ QUA (0)                │
├─────────────────────────────┤
│ Dashboard Today Goals Review │
└─────────────────────────────┘
```

| Thành phần | Quy định |
| --- | --- |
| Thanh tiến độ ngày | % Plan đã ghi nhận trong ngày |
| Nhóm Plan | Theo buổi (Sáng / Chiều / Tối) nếu Plan có giờ; nếu không thì theo Area |
| Plan Item chưa làm | Hiện target và nút **Ghi ›**; Plan Binary có công tắc ngay trên hàng |
| Plan Item đã ghi | Hiện Score, số sao, thu gọn; nhấn vào để sửa trong 24 giờ |
| Bỏ qua Plan | Vuốt trái hoặc menu ⋯ → "Bỏ qua hôm nay", chọn lý do (Bận, Ốm, Không muốn, Khác); XP đã kiếm trước đó không bị trừ (Business Rule 7) |
| Hôm nay không có Plan | "Ngày nghỉ. Muốn thêm một việc nhỏ không?" + nút Thêm Plan |
| Hoàn tất cả ngày | Hiệu ứng nhẹ, tóm tắt: Score trung bình của ngày + XP đã nhận |

### 7.2. Plan Result (ghi nhận kết quả)

**Mục đích:** ghi nhận một lần thực hiện Plan và cho người dùng thấy Score ngay. Mở dạng bảng trượt từ dưới lên (mobile) hoặc hộp thoại bên phải (desktop).

**Bước 1 — Nhập**

```text
┌─────────────────────────────┐
│ ✕   Ghi nhận                 │
│ Practice English             │
│ English · Mục tiêu: 30 phút  │
├─────────────────────────────┤
│ Thực tế                      │
│   [ − ]   25 phút   [ + ]    │
│   (hoặc [▶ Bấm giờ])         │
│                              │
│ Chất lượng                   │
│   ★ ★ ★ ★ ☆   Tốt            │
│                              │
│ Ghi chú (tuỳ chọn)           │
│ ┌─────────────────────────┐ │
│ │ Tập trung tốt hôm nay   │ │
│ └─────────────────────────┘ │
│                              │
│ [        Lưu kết quả       ] │
└─────────────────────────────┘
```

**Bước 2 — Kết quả (hiện ngay sau khi lưu)**

```text
┌─────────────────────────────┐
│           ( 89 )             │
│            Tốt               │
│         + 102 XP ↑ 4         │
├─────────────────────────────┤
│ Hoàn thành    83%  ████░     │
│ Chất lượng    80%  ████░     │
│ Nhất quán    100%  █████     │
│ Độ khó        Hard (×1.15)   │
├─────────────────────────────┤
│ English: Lv 5  ███░░ 2,189XP │
│ Còn 61 XP lên Level 6        │
├─────────────────────────────┤
│ [Sửa]            [Xong]      │
└─────────────────────────────┘
```

| Quy định | Chi tiết |
| --- | --- |
| Giá trị mặc định | Actual = Target; Quality = chưa chọn (bắt buộc chọn trước khi lưu) |
| Hiệu ứng Score | Vòng tròn chạy từ 0 đến Score trong 600ms; số XP bay lên cộng vào thanh Level |
| Phân rã điểm | 4 hàng (Hoàn thành, Chất lượng, Nhất quán, Độ khó) giúp người dùng hiểu vì sao có điểm này |
| Chú giải | Nhấn vào mỗi hàng để xem công thức và trọng số (40 / 25 / 20 / 15) |
| Score tối đa 100, XP có thể vượt 100 | Hiển thị "+103 XP" bên cạnh Score 90 khi Plan Hard (Business Rules 5, 6) |
| Sửa sau khi lưu | Được phép trong 24 giờ; Score tính lại và hiện chênh lệch |
| Lỗi lưu | Giữ nguyên dữ liệu đã nhập, hiện "Chưa lưu được. Thử lại", tự lưu nháp cục bộ |
| Vượt target | Actual lớn hơn Target vẫn tính Completion tối đa 100%; hiện "Bạn vượt mục tiêu 20%" |

## 8. Màn hình 6: Weekly Review

**Mục đích:** cuối mỗi tuần, cho người dùng một bản tổng kết ngắn để rút kinh nghiệm và điều chỉnh tuần sau (vòng lặp Plan → Do → Measure → Score → Review → Adjust). Báo cáo tự tạo vào Chủ nhật 20:00; người dùng nhận thông báo, mở xem trong 2–3 phút.

```text
┌───────────────────────────────────────────────────────────────┐
│ Weekly Review          Tuần 40 · 28/09 – 04/10   [‹] [›]      │
├───────────────────────────────────────────────────────────────┤
│  ┌────────┐   Hoàn thành       24 / 30 Plan                   │
│  │  (78)  │   Nhất quán        80%                            │
│  │ Tuần   │   XP               +620                           │
│  │ ↑ 6    │   Streak           14 ngày                        │
│  └────────┘                                                   │
├───────────────────────────────────────────────────────────────┤
│  Theo lĩnh vực                    Tuần này  Tuần trước        │
│  Career                           85 ↑      79                │
│  English                          72 ↑      68                │
│  Health                           91 →      91                │
│  Finance                          54 ↓      63   ⚠            │
├───────────────────────────────────────────────────────────────┤
│  Điểm nổi bật                     Cần chú ý                   │
│  ✓ Health giữ 91 điểm 3 tuần      ⚠ Finance giảm 9 điểm       │
│  ✓ English tăng 4 điểm            ⚠ Gym đạt 2/5 buổi          │
├───────────────────────────────────────────────────────────────┤
│  Gợi ý cho tuần sau                                           │
│  • Gym: giảm từ 5 xuống 3 buổi/tuần       [Áp dụng] [Bỏ qua]  │
│  • English Speaking: tăng 20 → 30 phút    [Áp dụng] [Bỏ qua]  │
├───────────────────────────────────────────────────────────────┤
│  Ghi chú tuần này (tuỳ chọn)                                  │
│  [ Điều gì đã giúp bạn / cản trở bạn tuần này?          ]     │
│                                       [ Hoàn tất review ]     │
└───────────────────────────────────────────────────────────────┘
```

| Khối | Nội dung | Hành vi |
| --- | --- | --- |
| Tóm tắt tuần | Score Ring, số Plan hoàn thành, Consistency, XP, Streak | So sánh với tuần trước bằng Trend Chip |
| Theo lĩnh vực | Bảng Area × (tuần này, tuần trước, xu hướng) | Nhấn vào hàng để mở Goal của Area |
| Điểm nổi bật / Cần chú ý | Tối đa 3 mục mỗi bên, tự phát hiện từ dữ liệu | Nhấn vào mục để xem Plan liên quan |
| Gợi ý | Đề xuất điều chỉnh target dựa trên completion rate (BRD mục 21) | **Áp dụng** sửa Plan ngay (có xác nhận); **Bỏ qua** ẩn gợi ý 4 tuần |
| Ghi chú | Ô văn bản tự do | Lưu cùng báo cáo, xem lại trong lịch sử |
| Hoàn tất review | Đánh dấu đã xem, ẩn banner trên Dashboard | Nếu chưa bấm, banner vẫn nhắc đến thứ Tư |

**Điều hướng tuần:** nút ‹ › chuyển giữa các tuần; tuần hiện tại chưa kết thúc hiện nhãn "Tạm tính".

**Chưa đủ dữ liệu:** dưới 3 ngày có log trong tuần thì ẩn Gợi ý và hiện "Cần thêm dữ liệu để đưa ra gợi ý".

## 9. Màn hình 7: Lịch biểu (Calendar)

**Mục đích:** đặt toàn bộ Plan, Milestone, deadline và Weekly Review lên trục thời gian tháng/tuần, để người dùng sắp lịch, thấy ngày quá tải và dời kế hoạch. Màn hình này đưa Calendar (BRD mục 26, vốn là Future) vào MVP ở dạng xem và sắp lịch nội bộ, chưa đồng bộ Google/Outlook Calendar.

&#91;image: Mockup màn hình Lịch biểu — Tháng 10/2026\]

Bố cục 3 vùng theo mockup: header + bộ lọc + 4 thẻ KPI ở trên; lưới tháng bên trái (khoảng 70%); panel ngày đã chọn bên phải (280–320px).

### 9.1. Header và bộ lọc

| Thành phần | Nội dung | Hành vi |
| --- | --- | --- |
| Nhãn chu kỳ | "Chu kỳ Quý IV · 2026", thời điểm cập nhật | Chỉ đọc |
| Điều hướng kỳ | ‹ Tháng 10, 2026 › và nút Hôm nay | ‹ › đổi tháng hoặc tuần theo chế độ xem; Hôm nay quay về và chọn ngày hiện tại |
| Chế độ xem | Tháng / Tuần / Danh sách | Nhớ lựa chọn gần nhất; mobile mặc định Danh sách |
| Nút chính | **+ Thêm Plan vào lịch** | Hộp thoại: chọn Plan có sẵn hoặc tạo mới, chọn ngày, giờ, lặp lại |
| Bộ lọc Area | Chip mỗi Area kèm số Plan trong kỳ, chip Tất cả | Chọn nhiều; màu theo mục 2.1 |
| Ẩn plan đã hoàn tất | Công tắc | Chỉ còn việc chưa làm trên lưới |

### 9.2. Thẻ KPI của kỳ

| Thẻ | Giá trị mẫu | Cách tính | Dòng phụ |
| --- | --- | --- | --- |
| Mục tiêu & Milestone | 5 mốc | Số Milestone có hạn trong kỳ | 2 Đạt · 1 At Risk |
| Kế hoạch lên lịch | 28 buổi | Tổng phiên Plan trong kỳ | Thanh tiến độ 18/28 xong |
| Tỷ lệ tuân thủ lịch | 88% | Phiên làm đúng ngày ÷ phiên đã đến hạn | +8% so với trung bình; ngưỡng cam kết 80% |
| Ngày tập trung cao điểm | Thứ 3 & Thứ 5 | Ngày trong tuần có Score trung bình cao nhất, cần ít nhất 4 tuần dữ liệu | Khối lượng trung bình 4 plans/ngày |

"Tỷ lệ tuân thủ lịch" là chỉ số mới, cần bổ sung vào BRD mục 23. Khác Consistency ở chỗ nó tính theo ngày đã lên lịch, không theo tần suất mục tiêu.

### 9.3. Lưới tháng

Tuần bắt đầu từ Thứ Hai; Chủ nhật đổi màu chữ vì là ngày Weekly Review; ngày ngoài tháng hiện mờ, không chứa dữ liệu.

```text
┌──────────────────────┐
│ 08   [Peak]       ✓  │  ← số ngày · nhãn đặc biệt · trạng thái
│ ▌AWS Solutions Arch… │  ← tối đa 3 chip, màu theo Area
│ ▌Shadowing           │
│ ▌Chạy 7km            │
│ + 2 khác             │
│ 4 plans · 2h30m      │  ← dòng tóm tắt
└──────────────────────┘
```

| Loại ô ngày | Dấu hiệu | Dòng tóm tắt |
| --- | --- | --- |
| Đã qua, hoàn thành hết | ✓ xanh lá ở góc | "3/3 plans" hoặc "100% hoàn tất" |
| Đã qua, còn thiếu | ! cam ở góc; chip chưa làm có viền đứt | "2/3 plans" |
| Hôm nay | Viền xanh dương 2px, nhãn HÔM NAY, nền trắng; chip đã xong có ✓ | "2/4 hoàn thành · 2 còn lại" |
| Tương lai | Nền xám nhạt | "2 plans lên lịch" |
| Ngày cao điểm | Nhãn Peak tím khi tổng thời lượng vượt 1,5 lần trung bình ngày | Tổng thời lượng, ví dụ "4 plans · 2h30m" |
| Deadline Milestone có rủi ro | Nền và viền cam, nhãn DEADLINE, chip Milestone đặc | Lý do, ví dụ "Cần nạp +3.5M · Finance At-Risk" |
| Milestone bình thường | Chip đặc màu Area, icon cờ | Mức chuẩn bị, ví dụ "Đã chuẩn bị 80%" |
| Chủ nhật | Chip Review xanh dương | "Tuần 42"; đã review thì ✓✓ và "Tuần 40 ✓" |

**3 loại chip:** chip Plan (nền nhạt, chữ màu Area), chip Milestone (nền đặc màu Area, chữ trắng, icon cờ), chip Review (xanh dương, icon xu hướng).

### 9.4. Panel ngày đã chọn

| Khối | Nội dung | Hành vi |
| --- | --- | --- |
| Header | "Ngày đã chọn", thứ và ngày, vị trí trong streak ("Ngày thứ 14 trong chuỗi") | Menu ⋯: Thêm Plan, Đánh dấu ngày nghỉ |
| Milestone trọng tâm | Milestone gần nhất liên quan đến ngày, tiến độ (3/5 buổi), thanh tiến độ | Nhấn vào mở Goal Detail |
| Lịch trình chi tiết | Tiêu đề "4 items · 2/4 đạt"; danh sách Plan Item theo giờ | Xem bảng dưới |
| Hạn chót kế tiếp | Deadline gần nhất và số ngày còn lại | Chữ đỏ khi còn 14 ngày trở xuống |
| Lên lại lịch | Nút Reschedule | Mở hộp thoại dời mọi Plan chưa làm của ngày sang ngày khác |
| Thẻ động lực tuần | Câu nhắc và tóm tắt streak | Tuỳ chọn, người dùng ẩn được trong Settings |

| Trạng thái Plan Item trong panel | Hiển thị | Hành động |
| --- | --- | --- |
| Đã xong | Ô tick đặc màu Area, tên gạch ngang, Score hoặc mức hoàn thành | Nhấn để xem hoặc sửa Plan Result |
| Sắp đến giờ (trong 60 phút) | Icon đồng hồ, khung giờ "20:00 – 20:45", viền nhấn | Nút **Bắt đầu**: mở bấm giờ, khi dừng chuyển sang Plan Result |
| Chưa đến giờ | Icon theo loại Plan, target, giờ | Nhấn để xem chi tiết |
| Bỏ qua | Chữ mờ, nhãn lý do | Hoàn tác trong ngày |

### 9.5. Tương tác

- Nhấn ô ngày để chọn, panel bên phải cập nhật; nhấn đúp vào ô tương lai để thêm Plan vào ngày đó.
- Rê chuột lên chip hiện tooltip: tên đầy đủ, target, giờ, Area.
- Nhấn chip Plan: hôm nay hoặc đã qua mở Plan Result; tương lai mở chi tiết lịch.
- Kéo thả chip sang ngày khác để dời lịch; hỏi "Chỉ lần này" hay "Lần này và các lần sau"; lý do dời là tuỳ chọn. Mỗi lần dời được tính vào Plan Reschedule Rate (BRD mục 23).
- Không kéo Plan vào ngày đã qua; không ghi nhận kết quả cho ngày tương lai.
- Nhấn chip Milestone mở Goal Detail, tab Milestones; nhấn chip Review mở Weekly Review của tuần đó.
- Phím tắt: ← → đổi ngày chọn, T về hôm nay, M / W / L đổi chế độ Tháng / Tuần / Danh sách.

### 9.6. Chế độ Tuần và Danh sách

- **Tuần:** 7 cột × trục giờ 06:00–23:00. Plan có giờ là khối cao theo thời lượng; Plan không có giờ nằm ở hàng "Cả ngày" trên cùng. Dùng để sắp giờ cụ thể.
- **Danh sách:** nhóm theo ngày, mỗi ngày là danh sách Plan Item như panel. Mặc định trên mobile.

### 9.7. Responsive

| Kích thước | Bố cục |
| --- | --- |
| Từ 1280px | Như mockup: lưới tháng + panel cố định bên phải |
| 1024–1279px | Panel thành ngăn trượt bên phải khi chọn ngày; thẻ KPI xếp 2×2 |
| Dưới 768px | Lưới tháng thu gọn: mỗi ô chỉ có số ngày và tối đa 4 chấm màu Area, vòng viền cho Hôm nay và Deadline; bên dưới là danh sách của ngày đã chọn; thẻ KPI cuộn ngang; vuốt ngang để đổi tháng |

### 9.8. Góp ý chỉnh mockup

- Đổi "Điểm AI: 92/100" thành "Score 92/100": Score do Score Engine tính (Business Rule 3), AI Coach chưa thuộc MVP.
- Thống nhất ngôn ngữ: sidebar dùng tiếng Việt (Tổng quan, Hôm nay, Mục tiêu, Đánh giá tuần) còn tài liệu dùng Dashboard, Today, Goals, Review. Nên chọn một.
- Ô Hôm nay bị cắt chữ nhiều ("Spea…30p", "Sy…20:00"): giới hạn 3 chip + "+N khác", bỏ thời lượng khỏi chip, để chi tiết ở panel.
- Plan Item đã xong vừa gạch ngang vừa có nhãn "Xong": giữ một dấu hiệu.
- Màu Health và Knowledge trong mockup gần giống nhau; dùng đúng mục 2.1 (Health #16A34A, Knowledge #0D9488) hoặc tách xa hơn.
- Chú thích dưới lưới nên có thêm Peak, Deadline và Review.

## 10. Trạng thái đặc biệt, responsive và accessibility

### 10.1. Responsive

| Breakpoint | Bố cục | Thay đổi chính |
| --- | --- | --- |
| Dưới 768px (mobile) | 1 cột, thanh tab dưới | Plan Result là bảng trượt toàn chiều rộng; biểu đồ rút gọn còn 4 tuần |
| 768–1023px (tablet) | 2 cột, sidebar thu gọn | Area Cards 2 cột |
| Từ 1024px (desktop) | Sidebar 240px + nội dung tối đa 1200px | Plan Result là hộp thoại 480px bên phải; Goal Detail có 2 cột |

Vùng chạm tối thiểu 44×44px trên mobile. Không để nội dung cuộn ngang trừ Area Cards trên mobile và bảng dữ liệu rộng.

### 10.2. Trạng thái dữ liệu

| Tình huống | Cách xử lý |
| --- | --- |
| Đang tải | Skeleton đúng kích thước từng khối; không dùng vòng quay toàn trang |
| Rỗng | Hình minh họa nhỏ + 1 câu giải thích + 1 nút hành động chính |
| Lỗi mạng | Banner trên cùng "Mất kết nối"; ghi nhận vẫn lưu cục bộ và đồng bộ khi có mạng |
| Lỗi từng khối | Thẻ lỗi tại chỗ kèm Thử lại; phần còn lại vẫn dùng được |
| Chưa đủ dữ liệu | Hiện số có nhãn "Tạm tính"; không hiện xu hướng khi dưới 7 ngày |
| Goal At Risk | Viền cam trên thẻ + Risk Banner trong Goal Detail |
| Goal quá hạn | Nhãn "Quá hạn N ngày" màu đỏ; nút Dời deadline hoặc Đóng Goal |

### 10.3. Accessibility

- Tương phản chữ tối thiểu 4.5:1, thành phần giao diện 3:1 (WCAG 2.2 AA).
- Mọi trạng thái và xu hướng có **icon + chữ**, không chỉ màu.
- Điều khiển được bằng bàn phím; thứ tự Tab theo thứ tự đọc; vòng focus rõ 2px.
- Star Rating có nhãn chữ và dùng được bằng phím mũi tên.
- Score Ring và biểu đồ có mô tả văn bản cho trình đọc màn hình ("Score 89 trên 100, mức Tốt, tăng 4 điểm").
- Tôn trọng cài đặt "giảm chuyển động": tắt hiệu ứng vòng chạy và XP bay.

### 10.4. Motion

| Hiệu ứng | Thời lượng | Dùng khi |
| --- | --- | --- |
| Vòng Score chạy từ 0 | 600ms | Sau khi lưu Plan Result |
| Tick hoàn thành | 150ms | Đánh dấu Plan Binary |
| Bảng trượt lên | 250ms | Mở Plan Result |
| Lên Level | 1200ms, một lần | Khi vượt ngưỡng XP của Level |

### 10.5. Microcopy

Giọng văn: ngắn, thân thiện, không phán xét. Ví dụ:

| Tình huống | Nên dùng | Tránh |
| --- | --- | --- |
| Score thấp | "Hôm nay hơi khó. Ngày mai thử lại nhé." | "Bạn đã thất bại." |
| Goal At Risk | "Tiến độ đang chậm hơn kế hoạch 17%." | "Bạn sắp trượt mục tiêu!" |
| Streak đứt | "Chuỗi 14 ngày kết thúc. Bắt đầu chuỗi mới hôm nay?" | "Bạn đã mất chuỗi." |
| Gợi ý giảm target | "Giảm xuống 3 buổi/tuần có thể phù hợp hơn." | "Mục tiêu của bạn quá cao." |

## 11. Business Rules trong UI, lộ trình và câu hỏi mở

### 11.1. Ánh xạ Business Rules sang giao diện

| Rule (BRD mục 24) | Cách UI thực thi |
| --- | --- |
| 1. Plan thuộc ít nhất một Goal | Form tạo Plan bắt buộc chọn Goal; Plan không thể lưu khi trống |
| 2. Plan phải có cách đo lường | Bước chọn loại Plan (5 loại) là bước bắt buộc, không có tuỳ chọn "không đo" |
| 3. User không nhập Score | Không có ô nhập Score ở bất kỳ màn hình nào; Score chỉ hiển thị |
| 4. Một Plan có nhiều Plan Log | Tab Lịch sử trong Goal Detail liệt kê mọi lần ghi nhận theo ngày |
| 5. Score tối đa 100 | Score Ring cắt ở 100; phần vượt target hiện bằng nhãn "Vượt mục tiêu" |
| 6. XP có thể vượt 100 | XP hiển thị riêng, ví dụ "+103 XP" cạnh Score 90 |
| 7. Bỏ Plan không mất XP đã có | Hộp thoại Bỏ qua/Lưu trữ ghi rõ "XP đã kiếm vẫn được giữ" |
| 8. Tiến độ Goal không chỉ đếm task | Tiến độ hiển thị kèm Score và vạch "kỳ vọng", không chỉ phần trăm task đã tick |

### 11.2. Lộ trình thiết kế

| Giai đoạn | Phạm vi | Sản phẩm bàn giao |
| --- | --- | --- |
| 1. Nền tảng | Design System, Component library | Bộ token, 8 component, trạng thái light/dark |
| 2. Luồng hằng ngày | Today, Plan Result | Prototype bấm được trên mobile, kiểm thử với 3–5 người dùng |
| 3. Mục tiêu | Goals, Goal Detail | Wireframe độ chi tiết cao, luồng tạo Goal/Milestone/Plan; màn hình Lịch biểu (Tháng, Tuần, Danh sách, kéo thả dời lịch) |
| 4. Phân tích | Dashboard, Weekly Review | Mock-up cuối, biểu đồ, trạng thái rỗng / thiếu dữ liệu |
| 5. Hoàn thiện | Responsive, accessibility, motion | Bản bàn giao cho dev: spec, asset, ghi chú tương tác |

**Sau MVP:** Achievements, thông báo, lịch, AI Coach, ứng dụng mobile / desktop riêng (BRD mục 26).

### 11.3. Câu hỏi mở

- [ ] Nền tảng ưu tiên đầu tiên là web responsive hay ứng dụng mobile native?
- [ ] Có cần đăng nhập và đồng bộ nhiều thiết bị ngay MVP, hay lưu cục bộ trước?
- [ ] Hiển thị Level và XP có nằm trên Dashboard cho mọi người dùng, hay cho phép tắt (người không thích gamification)?
- [ ] Ngôn ngữ giao diện: chỉ tiếng Việt, hay song ngữ Việt – Anh?
- [ ] Người dùng được sửa Plan Log sau 24 giờ không, hay khoá hoàn toàn để giữ dữ liệu đáng tin?
- [ ] Nhóm Area có cố định 6 nhóm hay người dùng tự tạo?

* [ ] Lịch biểu có cần đồng bộ hai chiều với Google / Outlook Calendar ở MVP, hay chỉ là lịch nội bộ?
* [ ] Có bổ sung "Tỷ lệ tuân thủ lịch" và "Ngày cao điểm" vào danh sách Business Metrics của BRD không?

### 11.4. Bước tiếp theo

Từ tài liệu này có thể làm tiếp: wireframe độ chi tiết cao cho Today và Plan Result, bộ Design Token đầy đủ, hoặc prototype tương tác.
