using System.Collections;
using System.Collections.Specialized;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Tracks whether a bound collection is currently empty, following INotifyCollectionChanged so the
///     answer stays correct as items come and go (the collections these controls bind are
///     ObservableCollections that get Cleared and refilled on every refresh, not replaced wholesale).
///
///     Shared by MasterDetailView and LinkedRecordsPanel: both need an empty state, and the
///     subscribe/unsubscribe bookkeeping is easy to get subtly wrong twice.
/// </summary>
internal sealed class CollectionEmptinessWatcher
{
    private readonly Action<bool> onEmptinessChanged;
    private INotifyCollectionChanged? subscribed;

    public CollectionEmptinessWatcher(Action<bool> onEmptinessChanged)
    {
        this.onEmptinessChanged = onEmptinessChanged;
    }

    public void Watch(IEnumerable? source)
    {
        if (subscribed is not null)
            subscribed.CollectionChanged -= OnCollectionChanged;

        subscribed = source as INotifyCollectionChanged;

        if (subscribed is not null)
            subscribed.CollectionChanged += OnCollectionChanged;

        Reevaluate(source);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Reevaluate(sender as IEnumerable);
    }

    private void Reevaluate(IEnumerable? source)
    {
        onEmptinessChanged(IsEmpty(source));
    }

    private static bool IsEmpty(IEnumerable? source)
    {
        if (source is null)
            return true;

        // ICollection covers every collection these controls actually bind and avoids walking the
        // whole sequence; the enumerator path is just a correctness fallback.
        if (source is ICollection collection)
            return collection.Count == 0;

        var enumerator = source.GetEnumerator();
        try
        {
            return !enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }
}
