# BRD — Personal Growth Tracker v1.0

Oct 6, 2026 · @Alex

## 1. Tóm tắt điều hành và vấn đề kinh doanh

Personal Growth Tracker biến mục tiêu phát triển bản thân dài hạn thành kế hoạch đo lường được, rồi chấm điểm từng lần thực hiện để người dùng biết **mình có đang tiến bộ không và tiến bộ nhanh đến đâu**. Khác với app Todo chỉ ghi "đã làm / chưa làm", hệ thống đo cả mức hoàn thành, chất lượng và độ đều đặn.

Vòng lặp nghiệp vụ cốt lõi: **Plan → Do → Measure → Score → Review → Adjust → Plan lại.**

### 1.1. Vấn đề cần giải quyết

| # | Vấn đề | Biểu hiện | Hệ quả |
| --- | --- | --- | --- |
| P1 | Có mục tiêu nhưng không có kế hoạch cụ thể | "Tôi muốn giỏi tiếng Anh" nhưng không biết học gì, bao nhiêu, bao lâu | Bỏ cuộc sau vài tuần |
| P2 | Todo list không cho thấy tiến bộ | Tick xong task nhưng không biết đóng góp bao nhiêu vào mục tiêu | Bận rộn nhưng không tiến lên |
| P3 | Không đánh giá được chất lượng | 25 phút tập trung và 30 phút mất tập trung đều là "Done" | Dữ liệu không phản ánh thực tế |
| P4 | Thiếu phản hồi dài hạn | Không biết mình yếu ở đâu, kế hoạch có quá sức không | Đặt quá nhiều mục tiêu, kiệt sức |
| P5 | Kế hoạch không gắn với thời gian | Không thấy ngày nào quá tải, deadline nào sắp đến | Dồn việc, trễ milestone |

### 1.2. Giải pháp tóm tắt

Hệ thống kết hợp 5 năng lực: quản lý mục tiêu theo cấp bậc (Vision → Goal → Milestone → Plan), lịch biểu để sắp và dời kế hoạch, ghi nhận kết quả thực tế, Score Engine tự tính điểm, và phân tích xu hướng kèm cảnh báo rủi ro.

## 2. Mục tiêu, chỉ số thành công và phạm vi

### 2.1. Mục tiêu kinh doanh

| Mã | Mục tiêu | Giải quyết vấn đề | Chỉ số thành công (đề xuất, cần chốt) |
| --- | --- | --- | --- |
| O1 | Goal Management: quản lý mục tiêu dài hạn theo cấp bậc | P1 | 80% người dùng mới tạo được ít nhất 1 Goal có Plan trong lần dùng đầu |
| O2 | Planning: chuyển mục tiêu thành kế hoạch đo lường được | P1, P5 | 100% Plan có cách đo lường; trung bình 3–5 Plan đang chạy mỗi người |
| O3 | Tracking: ghi nhận hành động thực tế nhanh | P2, P3 | Thời gian ghi nhận một Plan dưới 60 giây; 60% Plan đến hạn được ghi nhận |
| O4 | Scoring: đánh giá chất lượng thực hiện tự động | P3 | 100% Plan Log có Score; người dùng hiểu vì sao có điểm (khảo sát ≥ 4/5) |
| O5 | Analytics: phân tích xu hướng và cảnh báo sớm | P4 | 70% người dùng mở Weekly Review ít nhất 3/4 tuần |

**Chỉ số sức khoẻ sản phẩm:** tỷ lệ giữ chân người dùng tuần 4 (W4 retention) ≥ 30%, số ngày hoạt động trung bình ≥ 4 ngày/tuần.

### 2.2. Phạm vi MVP

| Trong phạm vi | Ngoài phạm vi (giai đoạn sau) |
| --- | --- |
| Đăng nhập, quản lý Area | AI Personal Coach, AI lập Goal, AI Weekly Review |
| Goal, Milestone, Plan (5 loại Plan) | Tự động điều chỉnh Plan không cần xác nhận |
| Ghi nhận Plan Log, Score Engine | Achievements, huy hiệu |
| XP, Level theo Area (hiển thị cơ bản) | Mạng xã hội, cộng đồng, bảng xếp hạng |
| Lịch biểu nội bộ: Tháng / Tuần / Danh sách, dời lịch | Đồng bộ Google / Outlook Calendar |
| Dashboard, Today, Weekly Review | Ứng dụng mobile và desktop native |
| Cảnh báo Goal / Plan At Risk, đề xuất điều chỉnh target theo quy tắc | Thông báo đẩy nâng cao, nhắc theo vị trí |

Vision được lưu như một mô tả định hướng trong MVP, chưa tính tiến độ.

## 3. Người dùng mục tiêu và các bên liên quan

Người dùng chính là **cá nhân tự quản lý quá trình phát triển bản thân**, theo đuổi nhiều lĩnh vực cùng lúc (Career, English, Health, Finance, Knowledge, Personal).

### 3.1. Persona

| Persona | Bối cảnh | Nhu cầu chính | Điều họ cần thấy |
| --- | --- | --- | --- |
| Developer đi làm (25–35 tuổi) | Muốn lên Senior trong 2 năm, học thêm tiếng Anh, giữ sức khoẻ | Cân bằng nhiều mục tiêu khi thời gian hạn chế | Lĩnh vực nào đang tụt, ngày nào quá tải |
| Sinh viên | Ôn thi chứng chỉ, học ngoại ngữ | Kế hoạch rõ ràng theo tuần, có deadline | Tiến độ so với ngày thi |
| Freelancer / Entrepreneur | Tự quản lý thời gian, không có sếp nhắc | Kỷ luật và phản hồi khách quan | Score và streak để giữ động lực |
| Người cải thiện sức khoẻ | Tập gym, ăn uống, ngủ | Theo dõi đều đặn, không quá sức | Consistency và gợi ý giảm target khi cần |

### 3.2. Các bên liên quan

| Bên liên quan | Vai trò | Quan tâm |
| --- | --- | --- |
| Product Owner | Chốt phạm vi, ưu tiên tính năng | Vòng lặp cốt lõi chạy được trước khi mở rộng |
| Business Analyst | Viết BRD, PRD, User Stories, đặc tả Score Engine | Quy tắc nghiệp vụ nhất quán, đo được |
| UI/UX Designer | Thiết kế 7 màn hình MVP | Ghi nhận nhanh, Score dễ hiểu |
| Developer | Xây dựng hệ thống | Công thức và quy tắc rõ ràng, không mơ hồ |
| Người dùng thử nghiệm | Dùng bản beta, phản hồi | App giúp họ tiến bộ thật, không thêm gánh nặng |

## 4. Mô hình khái niệm

Hệ thống có 7 khái niệm nghiệp vụ, đi từ định hướng dài hạn xuống từng lần thực hiện. Area là trục phân loại xuyên suốt, không phải một cấp bậc.

```text
Area (Career, English, Health, ...)
 │
Vision ─────── định hướng dài hạn, không bắt buộc deadline
 └── Goal ───── mục tiêu đo lường được, có ngày bắt đầu và ngày mục tiêu
      └── Milestone ── kết quả trung gian của Goal
           └── Plan ── hành động lặp lại, đo lường được, có lịch
                └── Plan Log ── một lần thực hiện: Actual, Quality, Note
                     └── Score + XP ── do hệ thống tính
```

### 4.1. Định nghĩa thực thể

| Khái niệm | Định nghĩa | Thuộc tính nghiệp vụ chính | Ví dụ |
| --- | --- | --- | --- |
| Area | Lĩnh vực phát triển, dùng để nhóm và tính Level | Tên, màu, thứ tự | Career |
| Vision | Định hướng dài hạn, chứa nhiều Goal | Tên, mô tả | Become a highly skilled software engineer |
| Goal | Mục tiêu đo lường được | Tên, mô tả, Area, Priority, Start Date, Target Date, Target, Progress, Status | Become Senior .NET Developer, 01/10/2026 → 01/10/2028 |
| Milestone | Kết quả trung gian của Goal | Tên, deadline, trọng số, Progress, Status | Master System Design |
| Plan | Đơn vị hành động có thể thực hiện và đo lường | Loại Plan, Target, Frequency, Duration, Difficulty, lịch | Study System Design, 45 phút/buổi, 3 buổi/tuần, 3 tháng |
| Plan Log (Plan Result) | Bản ghi một lần thực hiện Plan | Ngày, Actual, Quality 1–5, Note, trạng thái (Done / Skipped) | 25 phút, 4/5, "Tập trung tốt" |
| Weekly Review | Báo cáo tổng kết tuần | Score tuần, Completion, Consistency, XP, ghi chú | Tuần 40: 78/100 |

### 4.2. Quan hệ

| Quan hệ | Bản số | Ghi chú |
| --- | --- | --- |
| Vision – Goal | 1 – nhiều | Goal có thể không thuộc Vision nào |
| Goal – Milestone | 1 – nhiều | Goal không có Milestone vẫn hợp lệ |
| Milestone / Goal – Plan | nhiều – nhiều | Một Plan có thể đóng góp cho nhiều Goal (BR-01) |
| Plan – Plan Log | 1 – nhiều | BR-04 |
| Area – Goal | 1 – nhiều | Mỗi Goal thuộc đúng 1 Area |

### 4.3. Trạng thái Goal

| Trạng thái | Ý nghĩa | Ai thay đổi |
| --- | --- | --- |
| Draft | Đang soạn, chưa có Plan chạy | Người dùng |
| Active | Đã bắt đầu, chưa đủ dữ liệu đánh giá | Hệ thống, khi Plan đầu tiên được lên lịch |
| On Track | Tiến độ thực tế ≥ tiến độ kỳ vọng − 10 điểm % | Hệ thống |
| At Risk | Tiến độ thực tế thấp hơn kỳ vọng hơn 10 điểm % | Hệ thống |
| Completed | Đạt Target | Hệ thống đề xuất, người dùng xác nhận |
| Cancelled | Dừng theo đuổi | Người dùng |

Ngưỡng 10 điểm % là đề xuất ban đầu, cần chốt khi đặc tả Score Engine.

## 5. Yêu cầu nghiệp vụ

Các yêu cầu được đánh mã FR (Functional Requirement) để truy vết sang PRD và User Stories.

### 5.1. Quản lý mục tiêu và kế hoạch

| Mã | Yêu cầu | Ưu tiên |
| --- | --- | --- |
| FR-01 | Người dùng tạo, sửa, lưu trữ Area (mặc định 6 Area mẫu) | Must |
| FR-02 | Người dùng tạo Goal với đủ thuộc tính ở mục 4.1 | Must |
| FR-03 | Người dùng chia Goal thành Milestone, gán trọng số (mặc định bằng nhau) | Must |
| FR-04 | Người dùng tạo Plan, bắt buộc chọn loại Plan và Target | Must |
| FR-05 | Người dùng lưu Vision dạng mô tả và gắn Goal vào Vision | Should |

### 5.2. Năm loại Plan và cách đo

| Loại | Target | Người dùng nhập | Completion của một lần |
| --- | --- | --- | --- |
| Time-based | Số phút mỗi buổi | Số phút thực tế | min(Actual ÷ Target, 1) |
| Quantity-based | Số lượng mỗi buổi | Số lượng thực tế | min(Actual ÷ Target, 1) |
| Frequency-based | Số buổi mỗi tuần | Có / Không mỗi buổi | 100% khi làm; tần suất tính ở Consistency |
| Binary | Có / Không mỗi ngày | Có / Không | 100% hoặc 0% |
| Achievement-based | Danh sách tiêu chí hoàn thành | Tick tiêu chí | Số tiêu chí đạt ÷ tổng tiêu chí |

### 5.3. Score Engine

**FR-10:** mỗi Plan Log được hệ thống tự tính Score 0–100 theo 4 thành phần:

```latex
\text{Score} = 0.40 \cdot C + 0.25 \cdot Q + 0.20 \cdot S + 0.15 \cdot D
```

| Thành phần | Ký hiệu | Cách tính | Ví dụ |
| --- | --- | --- | --- |
| Completion | C | Theo bảng 5.2, quy ra 0–100 | 25/30 phút = 83 |
| Quality | Q | Số sao ÷ 5 × 100 | 4 sao = 80 |
| Consistency | S | Số buổi đã làm ÷ số buổi mục tiêu trong 7 ngày gần nhất, tối đa 100 | 4/5 buổi = 80 |
| Difficulty | D | Điểm theo mức khó (bảng dưới) | Hard = 90 |

Ví dụ BRD gốc: C = 90, Q = 80, S = 100, D = 90 → Score = 36 + 20 + 20 + 13,5 = 89,5, làm tròn **90**.

| Mức khó | Điểm D (đề xuất) | Hệ số XP |
| --- | --- | --- |
| Easy | 60 | ×1,00 |
| Normal | 75 | ×1,05 |
| Hard | 90 | ×1,15 |
| Extreme | 100 | ×1,30 |

Chỉ hệ số Hard ×1,15 có trong BRD gốc; các giá trị còn lại là đề xuất cần chốt (xem mục 9).

### 5.4. XP và Level

| Mã | Yêu cầu |
| --- | --- |
| FR-11 | XP của một Plan Log = Score × hệ số XP theo mức khó. Ví dụ Score 90, Hard → 103,5 XP |
| FR-12 | XP được cộng vào Area của Goal chứa Plan và vào tổng XP của người dùng |
| FR-13 | Level được tính theo ngưỡng XP tích luỹ, riêng cho từng Area và cho tổng thể |
| FR-14 | XP đã nhận không bị trừ khi Plan bị bỏ hoặc lưu trữ (BR-07) |

### 5.5. Progress

**FR-15:** tiến độ được tính từ dưới lên: Plan → Milestone → Goal → Area → Overall.

| Cấp | Cách tính (đề xuất) |
| --- | --- |
| Plan | Tổng Completion các lần đã làm ÷ tổng số buổi theo kế hoạch trong toàn Duration |
| Milestone | Trung bình có trọng số Progress các Plan thuộc Milestone |
| Goal | Trung bình có trọng số Progress các Milestone |
| Area | Trung bình Progress các Goal đang Active, theo Priority |
| Tiến độ kỳ vọng | Số ngày đã qua ÷ tổng số ngày từ Start Date đến Target Date |

Nhờ dùng Completion thay vì đếm task đã tick, tiến độ phản ánh mức độ làm thật (BR-08).

### 5.6. Lịch biểu

| Mã | Yêu cầu | Ưu tiên |
| --- | --- | --- |
| FR-20 | Hệ thống tự sinh các buổi Plan trên lịch theo Frequency và Duration | Must |
| FR-21 | Người dùng xem lịch theo Tháng, Tuần, Danh sách, lọc theo Area | Must |
| FR-22 | Deadline Milestone và Goal hiện trên lịch; ngày Chủ nhật hiện Weekly Review | Must |
| FR-23 | Người dùng dời một buổi hoặc mọi buổi sau sang ngày khác; ghi lại lý do (tuỳ chọn) | Must |
| FR-24 | Không lên lịch vào ngày đã qua; không ghi nhận kết quả cho ngày tương lai | Must |
| FR-25 | Hệ thống đánh dấu ngày cao điểm khi tổng thời lượng vượt 1,5 lần trung bình ngày | Should |

### 5.7. Ghi nhận hằng ngày

| Mã | Yêu cầu |
| --- | --- |
| FR-30 | Today liệt kê mọi buổi Plan của ngày, nhóm theo giờ hoặc Area |
| FR-31 | Người dùng ghi Actual, Quality, Note; Score và XP hiện ngay sau khi lưu |
| FR-32 | Người dùng bỏ qua một buổi kèm lý do (Bận, Ốm, Không muốn, Khác) |
| FR-33 | Plan Log được sửa trong 24 giờ; Score tính lại khi sửa |

### 5.8. Weekly Review

| Mã | Yêu cầu |
| --- | --- |
| FR-40 | Hệ thống tạo báo cáo tuần lúc 20:00 Chủ nhật: Score tuần, số Plan hoàn thành, Consistency, XP, Streak |
| FR-41 | Báo cáo so sánh Score từng Area với tuần trước và gắn xu hướng ↑ → ↓ |
| FR-42 | Xu hướng nhiều tuần được phân loại Improving / Stable / Declining |
| FR-43 | Người dùng thêm ghi chú và đánh dấu đã review |

### 5.9. Phát hiện rủi ro và đề xuất

| Mã | Điều kiện | Kết quả |
| --- | --- | --- |
| FR-50 | Tiến độ Goal thấp hơn kỳ vọng hơn 10 điểm % | Goal chuyển At Risk, hiện lý do. Ví dụ: kỳ vọng 55%, thực tế 38% |
| FR-51 | Plan có Consistency dưới 50% trong 14 ngày | Cảnh báo "Plan có thể quá khó". Ví dụ: 5/14 ngày = 36% |
| FR-52 | Completion rate dưới 60% trong 4 tuần | Đề xuất giảm target. Ví dụ: Gym 5 → 3 buổi/tuần khi chỉ đạt 48% |
| FR-53 | Consistency từ 85% trở lên trong 4 tuần | Đề xuất tăng target. Ví dụ: English Speaking 20 → 30 phút |
| FR-54 | Mọi đề xuất chỉ áp dụng khi người dùng xác nhận | Bỏ qua thì ẩn đề xuất 4 tuần |

Trong MVP, đề xuất chạy bằng quy tắc cố định ở trên; AI Coach thuộc giai đoạn sau.

## 6. Quy tắc nghiệp vụ

BR-01 đến BR-08 lấy từ BRD gốc; BR-09 đến BR-14 bổ sung để làm rõ Score, Lịch biểu và Review.

| Mã | Quy tắc | Lý do |
| --- | --- | --- |
| BR-01 | Plan phải thuộc ít nhất một Goal | Mọi hành động phải đóng góp vào một mục tiêu |
| BR-02 | Plan phải có cách đo lường (một trong 5 loại Plan và Target) | Không đo được thì không chấm điểm được |
| BR-03 | Người dùng không nhập trực tiếp Score; hệ thống tự tính từ dữ liệu thực tế | Giữ Score khách quan, so sánh được |
| BR-04 | Một Plan có thể có nhiều Plan Log | Plan lặp lại theo Frequency |
| BR-05 | Score của một Plan Log không vượt quá 100 | Score đo chất lượng, có trần |
| BR-06 | XP có thể vượt 100 | XP đo đóng góp, được nhân hệ số khó |
| BR-07 | Bỏ hoặc lưu trữ Plan không làm mất XP đã kiếm | Không phạt nỗ lực đã làm |
| BR-08 | Tiến độ Goal không chỉ dựa vào số task đã hoàn thành | Phản ánh mức độ làm thật (Completion) |
| BR-09 | Completion của một lần tối đa 100%; làm vượt Target được ghi nhận nhưng không cộng thêm điểm | Tránh dồn sức một ngày để bù |
| BR-10 | Buổi bị bỏ qua không có Score nhưng vẫn tính vào mẫu số Consistency | Consistency phản ánh đúng độ đều đặn |
| BR-11 | Plan Log chỉ sửa được trong 24 giờ sau khi tạo | Giữ dữ liệu lịch sử đáng tin |
| BR-12 | Dời lịch không thay đổi Score; mỗi lần dời được tính vào Plan Reschedule Rate | Tách việc sắp lịch khỏi chất lượng thực hiện |
| BR-13 | Chỉ số xu hướng và đề xuất chỉ hiện khi có ít nhất 7 ngày dữ liệu (đề xuất cần 4 tuần) | Tránh kết luận từ dữ liệu quá ít |
| BR-14 | Thay đổi Target từ đề xuất phải được người dùng xác nhận và chỉ áp dụng cho các buổi tương lai | Người dùng giữ quyền kiểm soát; không viết lại lịch sử |

## 7. Chỉ số nghiệp vụ

Hệ thống tính và lưu 4 nhóm chỉ số; hai chỉ số đánh dấu *mới* xuất phát từ màn hình Lịch biểu.

| Nhóm | Chỉ số | Định nghĩa | Hiển thị ở |
| --- | --- | --- | --- |
| Người dùng | Overall Score | Trung bình Score các Plan Log trong kỳ | Dashboard, Weekly Review |
| Người dùng | Overall XP, Overall Level | Tổng XP tích luỹ và Level tương ứng | Dashboard, sidebar |
| Người dùng | Current Streak, Longest Streak | Số ngày liên tiếp có ít nhất một Plan Log Done | Header, Today |
| Goal | Goal Progress | Mục 5.5 | Goals, Goal Detail |
| Goal | Goal Score | Trung bình Score các Plan Log thuộc Goal | Goal Detail |
| Goal | Goal Risk, Goal Trend | Trạng thái At Risk (FR-50) và xu hướng (FR-42) | Goals, Weekly Review |
| Goal | Goal Completion Rate | Số Goal Completed ÷ số Goal đã đến Target Date | Báo cáo quý |
| Plan | Completion Rate | Số buổi Done ÷ số buổi đã đến hạn | Goal Detail |
| Plan | Average Score, Quality | Trung bình Score và số sao | Goal Detail |
| Plan | Consistency | Mục 5.3 | Plan Result, Weekly Review |
| Hành vi | Daily / Weekly / Monthly Activity | Số ngày có hoạt động trong kỳ | Báo cáo nội bộ |
| Hành vi | Missed Plans | Số buổi đến hạn không có Plan Log | Weekly Review |
| Hành vi | Plan Reschedule Rate | Số buổi bị dời ÷ tổng số buổi đã lên lịch | Lịch biểu |
| Hành vi | Tỷ lệ tuân thủ lịch *(mới)* | Số buổi làm đúng ngày đã lên lịch ÷ số buổi đã đến hạn | Lịch biểu |
| Hành vi | Ngày cao điểm *(mới)* | Thứ trong tuần có Score trung bình cao nhất, cần ít nhất 4 tuần dữ liệu | Lịch biểu |

## 8. Lộ trình

MVP chỉ cần chứng minh vòng lặp Plan → Do → Measure → Score → Review chạy được và giữ chân người dùng; mọi thứ khác để sau.

| Giai đoạn | Nội dung | Điều kiện chuyển giai đoạn |
| --- | --- | --- |
| Phase 1 — MVP | Authentication → Area → Goal → Milestone → Plan → Plan Log → Score Engine → XP/Level cơ bản → Lịch biểu → Dashboard, Today, Weekly Review | W4 retention ≥ 30%; 60% Plan đến hạn được ghi nhận |
| Phase 2 — Gắn kết | Achievements, thông báo nhắc, Streak nâng cao, đồng bộ Google / Outlook Calendar | Đủ dữ liệu 8 tuần từ người dùng beta |
| Phase 3 — Thông minh | AI Personal Coach, AI Goal Planning, AI Weekly Review, tự động điều chỉnh Plan (vẫn cần xác nhận) | Quy tắc đề xuất của MVP được chấp nhận trên 40% |
| Phase 4 — Mở rộng | Ứng dụng mobile và desktop native, cộng đồng | Theo kết quả Phase 2–3 |

**7 màn hình MVP:** Dashboard, Goals, Goal Detail, Today, Plan Result, Weekly Review, Lịch biểu (chi tiết trong tài liệu UI Design).

**Tài liệu tiếp theo:** PRD → User Stories → Use Cases → đặc tả Score Engine → thiết kế database và API.

## 9. Giả định, ràng buộc, rủi ro và vấn đề cần chốt

### 9.1. Giả định và ràng buộc

- Người dùng sẵn sàng ghi nhận kết quả mỗi ngày nếu mất dưới 60 giây cho mỗi Plan.
- Quality do người dùng tự chấm 1–5 sao, nên mang tính chủ quan; hệ thống chấp nhận điều này ở MVP.
- MVP là sản phẩm cá nhân, một người dùng một tài khoản, chưa có chia sẻ hay nhóm.
- Múi giờ và thời điểm "kết thúc ngày" lấy theo thiết bị của người dùng.

### 9.2. Rủi ro

| Rủi ro | Ảnh hưởng | Giảm thiểu |
| --- | --- | --- |
| Ghi nhận hằng ngày gây mệt mỏi, người dùng bỏ app | Cao | Giá trị mặc định Actual = Target, ghi nhanh trên Today, Binary tick một chạm |
| Người dùng chấm Quality cao để "đẹp điểm" | Trung bình | Quality chỉ chiếm 25%; Completion và Consistency dựa trên dữ liệu thực tế |
| Công thức Score khó hiểu, người dùng mất niềm tin | Cao | Hiện phân rã 4 thành phần sau mỗi lần ghi nhận |
| Người dùng đặt quá nhiều Goal cùng lúc | Trung bình | Cảnh báo khi quá 5 Goal Active hoặc khi có ngày cao điểm lặp lại |
| Gamification (XP, Level) làm lệch động lực sang "cày điểm" | Thấp | Cho phép ẩn XP/Level; Score có trần 100 |

### 9.3. Vấn đề cần chốt trước khi viết PRD

- [ ] **Difficulty xuất hiện hai lần:** vừa là thành phần 15% của Score, vừa là hệ số nhân XP. Plan khó vì thế được cộng ở cả hai nơi, và Plan Easy luôn bị trừ điểm dù làm hoàn hảo (tối đa 94). Đề xuất: bỏ Difficulty khỏi Score, chia lại trọng số Completion 45%, Quality 30%, Consistency 25%, chỉ giữ Difficulty làm hệ số XP.
- [ ] Chốt điểm D và hệ số XP cho Easy, Normal, Extreme (hiện chỉ có Hard ×1,15).
- [ ] Công thức ngưỡng Level theo XP (ví dụ BRD: Career Level 8 với 4.250 XP, English Level 5 với 2.100 XP).
- [ ] Consistency tính theo cửa sổ 7 ngày trượt hay theo tuần lịch (Thứ Hai – Chủ nhật)?
- [ ] Ngưỡng At Risk 10 điểm % và các ngưỡng đề xuất (60%, 85%, 4 tuần) có phù hợp không?
- [ ] Một Plan đóng góp cho nhiều Goal thì XP và Progress chia thế nào?
- [ ] Vision có cần tính tiến độ ở giai đoạn sau không?
- [ ] Lịch biểu có cần đồng bộ hai chiều với Google / Outlook Calendar ngay MVP không?
