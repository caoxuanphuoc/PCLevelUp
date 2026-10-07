using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace GrowthTracker.Desktop.ViewModels;

/// <summary>Base for ViewModels that the ViewLocator turns into Views (pages and dialogs).</summary>
public abstract class ViewModelBase : ObservableObject;

/// <summary>A screen shown in the main content area. ViewModels call Application use cases, never the DbContext.</summary>
public abstract partial class PageViewModel : ViewModelBase
{
    public abstract string Title { get; }

    /// <summary>Placeholder text until the screen is built.</summary>
    public virtual string Subtitle => "";

    [ObservableProperty]
    public partial bool IsLoading { get; protected set; }

    [ObservableProperty]
    public partial string? LoadError { get; protected set; }

    /// <summary>Called on every navigation to this page, including Back, so data is always fresh.</summary>
    public virtual Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;

    /// <summary>Runs a load, tracking IsLoading and turning failures into a per-page error message.</summary>
    protected async Task LoadSafelyAsync(Func<Task> load)
    {
        IsLoading = true;
        LoadError = null;
        try
        {
            await load();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load page {Page}", GetType().Name);
            LoadError = "Không tải được dữ liệu. Hãy thử lại.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
