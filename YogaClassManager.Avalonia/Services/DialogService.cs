using ReactiveUI;
using YogaClassManager.Avalonia.ViewModels;

namespace YogaClassManager.Avalonia.Services;

public class DialogService : ReactiveObject, IDialogService
{
    private readonly List<object> stack = [];
    private object? activeDialog;

    public object? ActiveDialog
    {
        get => activeDialog;
        private set
        {
            this.RaisePropertyChanging(nameof(IsDialogOpen));
            this.RaiseAndSetIfChanged(ref activeDialog, value);
            this.RaisePropertyChanged(nameof(IsDialogOpen));
        }
    }

    public bool IsDialogOpen => ActiveDialog is not null;

    public async Task<TResult?> ShowDialogAsync<TResult>(DialogViewModelBase<TResult> viewModel)
    {
        var resultSource = new TaskCompletionSource<TResult?>();

        void OnRequestClose(TResult? result) => resultSource.TrySetResult(result);

        viewModel.RequestClose += OnRequestClose;
        stack.Add(viewModel);
        ActiveDialog = viewModel;

        try
        {
            return await resultSource.Task;
        }
        finally
        {
            viewModel.RequestClose -= OnRequestClose;
            stack.Remove(viewModel);
            ActiveDialog = stack.Count > 0 ? stack[^1] : null;
        }
    }
}
