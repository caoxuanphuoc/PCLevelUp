using CommunityToolkit.Mvvm.Input;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

public sealed partial class ConfirmDialogViewModel(string title, string message, string confirmText, bool isDestructive)
    : DialogViewModel<bool>
{
    public override string Title => title;
    public string Message => message;
    public string ConfirmText => confirmText;
    public bool IsDestructive => isDestructive;

    [RelayCommand]
    private void Confirm() => Close(true);
}
