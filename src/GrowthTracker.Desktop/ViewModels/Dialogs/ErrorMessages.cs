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
    };

    public static string For(Error error) => Messages.GetValueOrDefault(error.Code, error.Message);
}
