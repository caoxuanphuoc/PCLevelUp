using GrowthTracker.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop.Services;

public interface INavigationService
{
    PageViewModel? Current { get; }
    bool CanGoBack { get; }
    event EventHandler? CurrentChanged;

    void Navigate(Type pageType);
    void Navigate<TPage>() where TPage : PageViewModel;
    void GoBack();
}

/// <summary>Keeps a stack of page ViewModels; the ViewLocator turns the current one into its View.</summary>
public sealed class NavigationService(IServiceProvider services) : INavigationService
{
    private readonly Stack<PageViewModel> _history = new();

    public PageViewModel? Current { get; private set; }
    public bool CanGoBack => _history.Count > 0;
    public event EventHandler? CurrentChanged;

    public void Navigate<TPage>() where TPage : PageViewModel => Navigate(typeof(TPage));

    public void Navigate(Type pageType)
    {
        if (!typeof(PageViewModel).IsAssignableFrom(pageType))
            throw new ArgumentException($"{pageType.Name} is not a page view model.", nameof(pageType));
        if (Current?.GetType() == pageType)
            return;

        if (Current is not null)
            _history.Push(Current);
        SetCurrent((PageViewModel)services.GetRequiredService(pageType));
    }

    public void GoBack()
    {
        if (_history.TryPop(out var previous))
            SetCurrent(previous);
    }

    private void SetCurrent(PageViewModel page)
    {
        Current = page;
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }
}
