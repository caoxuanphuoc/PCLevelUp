using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

/// <summary>Shown for an unhandled UI exception (TDD §9.2): the error is logged and the user can open the log folder.</summary>
public sealed partial class ErrorDialogViewModel(string logDirectory, Action<string>? openFolder = null) : DialogViewModel<bool>
{
    public override string Title => "Đã xảy ra lỗi";
    public string Message => "Thao tác vừa rồi gặp lỗi ngoài dự kiến. Dữ liệu đã lưu không bị ảnh hưởng. "
                           + "Chi tiết lỗi đã được ghi vào file log; bạn có thể gửi file này khi báo lỗi.";
    public string LogDirectory => logDirectory;

    [RelayCommand]
    private void OpenLogs() => (openFolder ?? OpenInFileManager)(logDirectory);

    [RelayCommand]
    private void Dismiss() => Close(true);

    private static void OpenInFileManager(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not open the log folder");
        }
    }
}
