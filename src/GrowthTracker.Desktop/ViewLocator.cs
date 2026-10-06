using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GrowthTracker.Desktop.ViewModels;

namespace GrowthTracker.Desktop;

/// <summary>Maps a ViewModel to its View by name: ...ViewModels.Pages.TodayViewModel → ...Views.Pages.TodayView.</summary>
[RequiresUnreferencedCode("Resolves views by name through reflection.")]
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        string name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);
        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "View not found: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
