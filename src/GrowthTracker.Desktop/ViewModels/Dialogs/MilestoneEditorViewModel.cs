using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Goals;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

/// <summary>Adds a Milestone to a Goal (FR-03). Returns the Milestone id, or null when cancelled.</summary>
public sealed partial class MilestoneEditorViewModel(IUseCaseDispatcher dispatcher, Guid goalId, string goalName) : DialogViewModel<Guid?>
{
    public override string Title => "Thêm milestone";
    public string GoalName => goalName;

    [ObservableProperty] public partial string? Name { get; set; }
    [ObservableProperty] public partial DateTime? DueDate { get; set; }
    /// <summary>Relative weight inside the Goal; equal (1) by default.</summary>
    [ObservableProperty] public partial decimal? Weight { get; set; } = 1m;

    [RelayCommand]
    private Task Submit()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Hãy nhập tên milestone.";
            return Task.CompletedTask;
        }
        return SubmitAsync(
            () => dispatcher.SendAsync(new AddMilestoneCommand(goalId, Name,
                DueDate is { } due ? DateOnly.FromDateTime(due) : null, Weight ?? 1m)),
            id => (Guid?)id);
    }
}
