using CommunityToolkit.Mvvm.ComponentModel;

namespace GrowthTracker.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject;

/// <summary>A screen shown in the main content area. ViewModels call Application use cases, never the DbContext.</summary>
public abstract class PageViewModel : ViewModelBase
{
    public abstract string Title { get; }

    /// <summary>Placeholder text until the screen is built.</summary>
    public virtual string Subtitle => "";
}
