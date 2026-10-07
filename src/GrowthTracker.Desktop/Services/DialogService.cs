using GrowthTracker.Desktop.ViewModels.Dialogs;

namespace GrowthTracker.Desktop.Services;

public interface IDialogService
{
    /// <summary>The dialog currently shown in the overlay, or null.</summary>
    DialogViewModelBase? Current { get; }
    event EventHandler? CurrentChanged;

    Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog);

    Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false);
}

/// <summary>Shows one dialog at a time as an overlay in MainWindow; a dialog opened from a dialog stacks on top.</summary>
public sealed class DialogService : IDialogService
{
    private readonly Stack<DialogViewModelBase> _stack = new();

    public DialogViewModelBase? Current => _stack.TryPeek(out var top) ? top : null;
    public event EventHandler? CurrentChanged;

    public Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        _stack.Push(dialog);
        dialog.Closed += OnClosed;
        CurrentChanged?.Invoke(this, EventArgs.Empty);
        return dialog.Completion;

        void OnClosed(object? sender, EventArgs e)
        {
            dialog.Closed -= OnClosed;
            if (_stack.TryPeek(out var top) && ReferenceEquals(top, dialog))
                _stack.Pop();
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) =>
        await ShowAsync(new ConfirmDialogViewModel(title, message, confirmText, destructive));
}
