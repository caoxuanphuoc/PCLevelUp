using Avalonia.Threading;
using GrowthTracker.Desktop.Services;
using GrowthTracker.Desktop.ViewModels.Dialogs;
using GrowthTracker.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GrowthTracker.Desktop;

/// <summary>Logs every unhandled exception; UI-thread errors also show "Đã xảy ra lỗi" with a link to the logs (TDD §9.2).</summary>
internal static class GlobalExceptionHandlers
{
    public static void RegisterProcessWide()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception (AppDomain)");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    public static void RegisterUiThread(IServiceProvider services)
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log.Error(e.Exception, "Unhandled exception (UI thread)");
            e.Handled = true;
            ShowErrorDialog(services);
        };
    }

    /// <summary>One error dialog at a time: a burst of failures does not stack dialogs.</summary>
    internal static void ShowErrorDialog(IServiceProvider services)
    {
        var dialogs = services.GetRequiredService<IDialogService>();
        if (dialogs.Current is ErrorDialogViewModel)
            return;
        _ = dialogs.ShowAsync(new ErrorDialogViewModel(services.GetRequiredService<AppPaths>().LogDirectory));
    }
}
