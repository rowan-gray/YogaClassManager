using ReactiveUI;

namespace YogaClassManager.Avalonia.ViewModels;

/// <summary>
///     Ports BasePageModel's reentrant busy-depth counter from the MAUI app verbatim: multiple
///     overlapping async operations can each call StartBusy/EndBusy without the flag flickering off
///     early, which a single ReactiveCommand.IsExecuting stream can't express on its own.
/// </summary>
public abstract class ViewModelBase : ReactiveObject
{
    /// <summary>
    ///     How long a fetch waits after the trigger before it runs - when a page opens (its first load) or
    ///     the selection changes in a list-and-details layout (the details). The skeleton shows during the
    ///     wait. Clicking quickly through pages or records then only fetches for the one you stop on:
    ///     each new selection cancels the previous wait, and a page you've already left skips its fetch.
    ///     Zero turns it off (the test project does, so its loads stay synchronous).
    /// </summary>
    public static TimeSpan LoadDebounce { get; set; } = TimeSpan.FromMilliseconds(200);

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
    /// <summary>
    ///     Waits out <see cref="LoadDebounce" />, then says whether to go ahead and fetch: false if this is
    ///     a routed page that's no longer the current one (the user navigated on during the wait). A
    ///     page not in a router at all (e.g. constructed directly) always goes ahead.
    /// </summary>
    protected async Task<bool> DebounceLoadAsync(CancellationToken cancellationToken = default)
    {
        if (LoadDebounce <= TimeSpan.Zero)
            return true;

        await Task.Delay(LoadDebounce, cancellationToken);

        if (this is not IRoutableViewModel page || page.HostScreen?.Router is not { } router ||
            router.NavigationStack.Count == 0)
            return true;

        return ReferenceEquals(router.GetCurrentViewModel(), page);
    }

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
                // The full exception goes to the error log; onError decides what the user is told.
                Services.ErrorLog.Error($"Couldn't load {GetType().Name}", exception);
                onError(exception);
            }
        }
    }
}
