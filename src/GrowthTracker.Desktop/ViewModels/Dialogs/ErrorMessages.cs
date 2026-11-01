using GrowthTracker.Domain.Common;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

/// <summary>Vietnamese messages for domain/application error codes; falls back to the English message.</summary>
public static class ErrorMessages
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["area.name_required"] = "Hãy nhập tên lĩnh vực.",
        ["area.color"] = "Màu không hợp lệ.",
        ["area.last_active"] = "Cần giữ lại ít nhất một lĩnh vực đang dùng.",
        ["area.not_found"] = "Không tìm thấy lĩnh vực.",
        ["goal.name_required"] = "Hãy nhập tên mục tiêu.",
        ["goal.dates"] = "Ngày mục tiêu phải sau ngày bắt đầu.",
        ["goal.priority"] = "Độ ưu tiên không hợp lệ.",
        ["goal.not_found"] = "Không tìm thấy mục tiêu.",
        ["goal.closed"] = "Mục tiêu đã hoàn thành hoặc đã huỷ.",
        ["milestone.name_required"] = "Hãy nhập tên milestone.",
        ["milestone.weight"] = "Trọng số phải lớn hơn 0.",
        ["milestone.not_found"] = "Không tìm thấy milestone.",
        ["plan.name_required"] = "Hãy nhập tên kế hoạch.",
        ["plan.duration"] = "Ngày kết thúc phải sau ngày bắt đầu.",
        ["plan.goal_required"] = "Kế hoạch phải thuộc ít nhất một mục tiêu.",
        ["plan.target_required"] = "Hãy nhập target lớn hơn 0.",
        ["plan.frequency_required"] = "Chọn từ 1 đến 7 buổi mỗi tuần.",
        ["plan.criteria_required"] = "Thêm ít nhất một tiêu chí hoàn thành.",
        ["criterion.title_required"] = "Tiêu chí không được để trống.",
        ["plan.not_found"] = "Không tìm thấy kế hoạch.",
        ["occurrence.not_found"] = "Không tìm thấy buổi này.",
        ["occurrence.past_date"] = "Không thể xếp lịch vào ngày đã qua.",
        ["occurrence.not_scheduled"] = "Chỉ dời được những buổi chưa thực hiện.",
        ["plan.not_active"] = "Kế hoạch đã lưu trữ hoặc tạm dừng.",
        ["plan.end_in_past"] = "Ngày kết thúc mới không được trước hôm nay.",
        ["plan.ended"] = "Kế hoạch đã kết thúc nên không còn buổi nào để áp dụng. Hãy sửa kế hoạch để kéo dài thời gian nếu muốn tiếp tục.",
        ["plan.session_after_end"] = "Ngày này nằm sau ngày kết thúc của kế hoạch. Hãy sửa kế hoạch để kéo dài thời gian trước.",
        ["recommendation.not_found"] = "Không tìm thấy gợi ý.",
        ["recommendation.not_pending"] = "Gợi ý này đã được xử lý.",
        ["occurrence.future_date"] = "Chưa thể bỏ qua buổi trong tương lai.",
        ["occurrence.already_done"] = "Buổi này đã có kết quả nên không thể bỏ qua.",
        ["planlog.not_found"] = "Không tìm thấy kết quả đã ghi.",
        ["planlog.already_logged"] = "Buổi này đã được ghi nhận.",
        ["planlog.future_date"] = "Không thể ghi nhận cho ngày trong tương lai.",
        ["planlog.edit_window"] = "Đã quá 24 giờ nên không thể sửa kết quả này nữa.",
        ["planlog.quality"] = "Hãy chấm chất lượng từ 1 đến 5 sao.",
    };

    public static string For(Error error) => Messages.GetValueOrDefault(error.Code, error.Message);
}
