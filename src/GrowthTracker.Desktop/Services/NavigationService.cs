using GrowthTracker.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Desktop.Services;

public interface INavigationService
{
    PageViewModel? Current { get; }
    bool CanGoBack { get; }
    event EventHandler? CurrentChanged;

    /// <param name="parameter">Passed to <see cref="PageViewModel.OnNavigatedToAsync"/>, e.g. a Goal id.</param>
    Task NavigateAsync(Type pageType, object? parameter = null);
    Task NavigateAsync<TPage>(object? parameter = null) where TPage : PageViewModel;
    Task GoBackAsync();
}

/// <summary>Keeps a stack of page ViewModels; the ViewLocator turns the current one into its View.</summary>
public sealed class NavigationService(IServiceProvider services) : INavigationService
{
    private readonly Stack<(PageViewModel Page, object? Parameter)> _history = new();
    private object? _currentParameter;

    public PageViewModel? Current { get; private set; }
    public bool CanGoBack => _history.Count > 0;
    public event EventHandler? CurrentChanged;

    public Task NavigateAsync<TPage>(object? parameter = null) where TPage : PageViewModel =>
        NavigateAsync(typeof(TPage), parameter);

    public Task NavigateAsync(Type pageType, object? parameter = null)
    {
        if (!typeof(PageViewModel).IsAssignableFrom(pageType))
            throw new ArgumentException($"{pageType.Name} is not a page view model.", nameof(pageType));
        if (Current?.GetType() == pageType && Equals(_currentParameter, parameter))
            return Current.OnNavigatedToAsync(parameter);

        if (Current is not null)
            _history.Push((Current, _currentParameter));
        return ShowAsync((PageViewModel)services.GetRequiredService(pageType), parameter);
    }

    public Task GoBackAsync() =>
        _history.TryPop(out var previous) ? ShowAsync(previous.Page, previous.Parameter) : Task.CompletedTask;

    private Task ShowAsync(PageViewModel page, object? parameter)
    {
        Current = page;
        _currentParameter = parameter;
        CurrentChanged?.Invoke(this, EventArgs.Empty);
        return page.OnNavigatedToAsync(parameter);
    }
}
