using Avalonia.Threading;
using Serilog;

namespace GrowthTracker.Desktop;

/// <summary>Logs every unhandled exception. The user-facing "an error occurred" dialog comes with DialogService.</summary>
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

    public static void RegisterUiThread()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log.Error(e.Exception, "Unhandled exception (UI thread)");
            e.Handled = true;
        };
    }
}
