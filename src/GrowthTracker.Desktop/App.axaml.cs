using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GrowthTracker.Desktop.ViewModels;
using GrowthTracker.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop;

public partial class App : Avalonia.Application
{
    /// <summary>Root service provider, set by Program (or by tests) before Avalonia starts.</summary>
    public static IServiceProvider? Services { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Services is not null)
        {
            GlobalExceptionHandlers.RegisterUiThread();
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
