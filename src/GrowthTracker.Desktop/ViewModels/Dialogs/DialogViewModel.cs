using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Domain.Common;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

/// <summary>Non-generic face of a dialog, used by the overlay host.</summary>
public abstract partial class DialogViewModelBase : ViewModelBase
{
    public abstract string Title { get; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; protected set; }

    [ObservableProperty]
    public partial bool IsBusy { get; protected set; }

    [RelayCommand]
    public void Cancel() => CloseWithoutResult();

    protected abstract void CloseWithoutResult();
}

/// <summary>A dialog that completes a Task with a result (default when cancelled), per TDD §6.2.</summary>
public abstract class DialogViewModel<TResult> : DialogViewModelBase
{
    private readonly TaskCompletionSource<TResult?> _completion = new();

    public Task<TResult?> Completion => _completion.Task;

    public event EventHandler? Closed;

    protected void Close(TResult? result)
    {
        if (_completion.TrySetResult(result))
            Closed?.Invoke(this, EventArgs.Empty);
    }

    protected override void CloseWithoutResult() => Close(default);

    /// <summary>Runs a use case; on failure keeps the dialog open and shows the error inline.</summary>
    protected async Task SubmitAsync<T>(Func<Task<Result<T>>> send, Func<T, TResult> onSuccess)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await send();
            if (result.IsSuccess)
                Close(onSuccess(result.Value!));
            else
                ErrorMessage = ErrorMessages.For(result.Error!);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
