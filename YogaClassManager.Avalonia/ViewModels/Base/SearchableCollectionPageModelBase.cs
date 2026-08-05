using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;

namespace YogaClassManager.Avalonia.ViewModels.Base;

/// <summary>
///     Adds debounced live-as-you-type search on top of CollectionPageModelBase. This is a deliberate
///     upgrade over the MAUI app's SearchableCollectionPageModel&lt;T&gt;, which had no debounce at all
///     and only searched when a Search command/button was explicitly invoked.
/// </summary>
public abstract class SearchableCollectionPageModelBase<T> : CollectionPageModelBase<T>
{
    private string? searchQuery;

    protected SearchableCollectionPageModelBase(IToastService toastService) : base(toastService)
    {
        // WhenAnyValue emits the current value immediately on subscription (SearchQuery's initial
        // null), not just on later changes - without Skip(1), that phantom "change" rides the same
        // throttle into a redundant RefreshCommand ~300ms after every page load, rebuilding the
        // already-correctly-populated list for no reason (and visibly glitching the list's
        // selection while it does - see the ListBox rebuild it causes).
        this.WhenAnyValue(x => x.SearchQuery)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)
            .DistinctUntilChanged()
            .Select(_ => System.Reactive.Unit.Default)
            .InvokeCommand(RefreshCommand);
    }

    public string? SearchQuery
    {
        get => searchQuery;
        set => this.RaiseAndSetIfChanged(ref searchQuery, value);
    }
}
