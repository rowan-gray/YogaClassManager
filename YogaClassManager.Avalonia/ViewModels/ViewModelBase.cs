using ReactiveUI;

namespace YogaClassManager.Avalonia.ViewModels;

/// <summary>
///     Ports BasePageModel's reentrant busy-depth counter from the MAUI app verbatim: multiple
///     overlapping async operations can each call StartBusy/EndBusy without the flag flickering off
///     early, which a single ReactiveCommand.IsExecuting stream can't express on its own.
/// </summary>
public abstract class ViewModelBase : ReactiveObject
{
    private int busyDepth;
    private bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            this.RaisePropertyChanging(nameof(IsNotBusy));
            this.RaiseAndSetIfChanged(ref isBusy, value);
            this.RaisePropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public void StartBusy()
    {
        if (busyDepth == 0)
            IsBusy = true;
        busyDepth++;
    }

    public void EndBusy()
    {
        busyDepth--;
        if (busyDepth == 0)
            IsBusy = false;
    }

    /// <summary>
    ///     Kicks off an initial load from a constructor and routes any failure to
    ///     <paramref name="onError" />.
    ///
    ///     A constructor can't await, so these loads have to be fire-and-forget - but a bare
    ///     <c>_ = LoadAsync()</c> makes the failure an unobserved Task exception, thrown away silently
    ///     and leaving a permanently empty list or dropdown with no explanation (UI_REVIEW.md A3).
    ///     PassEditViewModel was the worst case: a failed term load left Terms empty, and Save then told
    ///     the user to "Select a term and a class" - blaming them for a load failure.
    ///
    ///     onError is a callback rather than a fixed toast because the right surface differs: a page
    ///     toasts, a dialog puts the message on its own validation line, right beside the empty control.
    /// </summary>
    protected void LoadOnCreate(Func<Task> load, Action<Exception> onError)
    {
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                await load();
            }
            catch (Exception exception)
            {
                onError(exception);
            }
        }
    }
}
