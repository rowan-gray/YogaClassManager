using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Core.Data;

namespace YogaClassManager.Avalonia.ViewModels.Base;

/// <summary>
///     Translates the MAUI app's CollectionPageModel&lt;T&gt; into ReactiveUI: a list-backed page with a
///     refresh command wired to the busy-depth counter, and error surfacing via ThrownExceptions
///     instead of a try/catch repeated in every override.
///
///     It also owns the three pieces every list page was otherwise re-deriving by hand:
///     <see cref="HasSelection" />, the ascending/descending <see cref="SortOrderOptions" /> list, and
///     the "reload when a filter changes" chain (<see cref="RefreshWhenChanged{TSignal}" />) whose
///     mandatory Skip(1) was previously spelled out five times.
/// </summary>
public abstract class CollectionPageModelBase<T> : ViewModelBase
{
    private readonly IToastService toastService;
    private T? selection;
    private string? lastError;
    private int detailLoadsInFlight;
    private bool hasLoadedOnce;

    protected CollectionPageModelBase(IToastService toastService)
    {
        this.toastService = toastService;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

        // Busy is set inside RefreshAsync itself, synchronously, rather than from RefreshCommand.IsExecuting:
        // IsExecuting arrives via the UI scheduler a tick later, so during the load debounce the page could
        // sit there with no skeleton showing.

        // Subscribing to ThrownExceptions at all means ReactiveUI stops routing these to
        // RxApp.DefaultExceptionHandler (Services/ToastExceptionHandler.cs) - so this subscription has
        // to do the surfacing itself, or a failed load is silent. Both surfaces are deliberate: the
        // toast is transient acknowledgement, LastError persists in the list pane (MasterDetailView's
        // ErrorText) so the user isn't left staring at an empty list wondering if it's empty or broken.
        RefreshCommand.ThrownExceptions.Subscribe(ex =>
        {
            ErrorLog.Error($"Couldn't load the list on {GetType().Name}", ex);
            LastError = ex.Message;
            toastService.ShowError($"Couldn't load this list: {ex.Message}");
        });

        HasSelection = this.WhenAnyValue(x => x.Selection).Select(item => item is not null);
    }

    public ObservableCollection<T> Items { get; } = new();

    public T? Selection
    {
        get => selection;
        set => this.RaiseAndSetIfChanged(ref selection, value);
    }

    public string? LastError
    {
        get => lastError;
        protected set => this.RaiseAndSetIfChanged(ref lastError, value);
    }

    /// <summary>True while any <see cref="ReloadOn{TSignal}" /> load (the detail pane's linked lists)
    /// is running - drives MasterDetailView's detail-pane progress bar, so a student's passes or a
    /// class's rolls read as "loading" rather than "empty" while they're fetched. A count, not a bool:
    /// several panes load at once, and a superseded load's finally still runs.</summary>
    public bool IsLoadingDetails => Volatile.Read(ref detailLoadsInFlight) > 0;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>The ascending/descending pair every SortByButton binds. Identical on all five list
    /// pages, so it lives here rather than being re-declared per page.</summary>
    public IReadOnlyList<Order> SortOrderOptions { get; } = Enum.GetValues<Order>();

    /// <summary>Gates every command that acts on the selected record. Exposed as an observable rather
    /// than a bool because that is what ReactiveCommand's canExecute takes.</summary>
    protected IObservable<bool> HasSelection { get; }

    protected abstract Task<IReadOnlyList<T>> LoadItemsAsync();

    /// <summary>
    ///     Reloads the list whenever one of the supplied filter/sort properties changes.
    ///
    ///     The Skip(1) is the point: WhenAnyValue replays current values on subscription, and without
    ///     it every page would fire a redundant reload immediately after construction, on top of the
    ///     initial load it already does - visibly resetting the selection as it goes.
    /// </summary>
    protected IDisposable RefreshWhenChanged<TSignal>(IObservable<TSignal> changes)
    {
        return changes
            .Skip(1)
            .Select(_ => Unit.Default)
            .InvokeCommand(RefreshCommand);
    }

    /// <summary>
    ///     Reloads a selection-dependent child list (linked passes, emergency contacts, rolls) whenever
    ///     the supplied trigger fires.
    ///
    ///     Switch, not SelectMany: SelectMany merges concurrent inner tasks, so two quick selection
    ///     changes run two loads that each Clear() and refill the same ObservableCollection - last
    ///     writer wins, and an interleaved Clear() part-way through the other's foreach can leave one
    ///     record's linked data showing under another's header.
    ///
    ///     Switch on its own is not enough, though, and this is the part worth remembering: these loads
    ///     mutate their collections in place rather than returning a value, so discarding a superseded
    ///     load's *result* changes nothing - its writes still land. That is why <paramref name="load" />
    ///     takes a CancellationToken. Rx cancels it when Switch drops the previous inner observable, and
    ///     each load is expected to pass it to its repository calls and re-check it before writing.
    ///
    ///     Error handling is the other half, and the placement is the subtle part: the Catch sits
    ///     *inside* the inner observable, not on the outer Subscribe. An error that reaches Switch
    ///     terminates the whole outer sequence, so handling it as the subscriber's onError would report
    ///     the failure and still leave the pane permanently dead - the original problem, just with a
    ///     toast. Catching per-load and continuing with an empty sequence keeps the trigger live.
    ///     A cancelled load is not a failure and is not reported.
    /// </summary>
    /// <param name="debounce">Wait <see cref="ViewModelBase.LoadDebounce" /> before loading - for a
    /// selection trigger, so clicking quickly through records only loads the one you stop on. Each new
    /// trigger cancels the previous wait (Switch).</param>
    /// <param name="clearNow">Runs the moment the trigger fires, before any wait: empty the lists the
    /// load fills, so the previous record's details vanish and the skeletons show straight away.</param>
    protected IDisposable ReloadOn<TSignal>(IObservable<TSignal> trigger, Func<CancellationToken, Task> load,
        string whatFailed, bool debounce = false, Action? clearNow = null)
    {
        return trigger
            .Do(_ => clearNow?.Invoke())
            .Select(_ => Observable.FromAsync(async cancellationToken =>
            {
                Interlocked.Increment(ref detailLoadsInFlight);
                this.RaisePropertyChanged(nameof(IsLoadingDetails));
                try
                {
                    if (debounce && LoadDebounce > TimeSpan.Zero)
                        await Task.Delay(LoadDebounce, cancellationToken);
                    await load(cancellationToken);
                }
                finally
                {
                    Interlocked.Decrement(ref detailLoadsInFlight);
                    this.RaisePropertyChanged(nameof(IsLoadingDetails));
                }
            }).Catch<Unit, Exception>(error =>
            {
                if (error is not OperationCanceledException)
                {
                    ErrorLog.Error($"Couldn't load {whatFailed} on {GetType().Name}", error);
                    LastError = error.Message;
                    toastService.ShowError($"Couldn't load {whatFailed}: {error.Message}");
                }

                return Observable.Empty<Unit>();
            }))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe();
    }

    private async Task RefreshAsync()
    {
        StartBusy();
        try
        {
            await RefreshCoreAsync();
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task RefreshCoreAsync()
    {
        // The page's first load waits out the debounce (the list's skeleton is already showing - the
        // refresh counts as busy) and is skipped if the user has already moved on to another page.
        if (!hasLoadedOnce)
        {
            hasLoadedOnce = true;
            if (!await DebounceLoadAsync())
                return;
        }

        var items = await LoadItemsAsync();

        var previousSelection = Selection;

        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        Selection = previousSelection is not null && Items.Contains(previousSelection)
            ? previousSelection
            : Items.FirstOrDefault();
    }
}
