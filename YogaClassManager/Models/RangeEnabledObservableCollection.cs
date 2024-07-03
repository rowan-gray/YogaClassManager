using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace YogaClassManager.Models;

public class RangeEnabledObservableCollection<T> : ObservableCollection<T>
{
    public RangeEnabledObservableCollection()
    {
    }

    public RangeEnabledObservableCollection(List<T> list) : base(list)
    {
    }

    public RangeEnabledObservableCollection(IEnumerable<T> collection) : base(collection)
    {
    }

    public void AddRange(IEnumerable<T> items)
    {
        CheckReentrancy();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(EventArgsCache.CountPropertyChanged);
        OnPropertyChanged(EventArgsCache.IndexerPropertyChanged);
        OnCollectionChanged(EventArgsCache.ResetCollectionChanged);
    }

    internal static class EventArgsCache
    {
        internal static readonly PropertyChangedEventArgs CountPropertyChanged = new("Count");
        internal static readonly PropertyChangedEventArgs IndexerPropertyChanged = new("Item[]");

        internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged =
            new(NotifyCollectionChangedAction.Reset);
    }
}