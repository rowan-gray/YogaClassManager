using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace YogaClassManager.NewModels;

public class GrowableDbCollection<T, TModel, TFilter>
    : ObservableCollection<TModel>, IGrowableCollection where T : IDbModel<TModel, TFilter>
{
    private readonly TFilter filter;
    private readonly uint growAmount;

    public GrowableDbCollection(TFilter filter, uint growAmount)
    {
        this.filter = filter;
        this.growAmount = growAmount;
    }

    public async Task GrowCollection(uint amount)
    {
        var models = await
            T.LoadMultiple(filter, growAmount, Convert.ToUInt32(Count));

        InsertRange(models);
    }

    public void InsertRange(IEnumerable<TModel> items)
    {
        CheckReentrancy();
        foreach (var item in items)
            Items.Add(item);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}