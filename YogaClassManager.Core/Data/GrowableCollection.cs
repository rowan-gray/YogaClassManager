using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace YogaClassManager.Core.Data;

/// <summary>
///     An ObservableCollection that pages in more of its backing IDbModel's results on demand.
///     Works identically whether TDbModel is backed by an in-memory store or a real database.
/// </summary>
public class GrowableCollection<TModel, TFilter> : ObservableCollection<TModel>, IGrowableCollection
{
    private readonly IDbModel<TModel, TFilter> dbModel;

    public GrowableCollection(IDbModel<TModel, TFilter> dbModel)
    {
        this.dbModel = dbModel;
    }

    public async Task<uint> GrowCollection(uint amount)
    {
        var models = await dbModel.LoadMultiple(amount, Convert.ToUInt32(Count));

        InsertRange(models);

        return Convert.ToUInt32(models.Count);
    }

    public void InsertRange(IReadOnlyCollection<TModel> items)
    {
        CheckReentrancy();

        var startIndex = Count;

        foreach (var item in items)
            Items.Add(item);

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
            items.ToList(), startIndex));
        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
    }
}
