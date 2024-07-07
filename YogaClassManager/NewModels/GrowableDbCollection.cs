using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using YogaClassManager.NewDatabase;

namespace YogaClassManager.NewModels;

public class GrowableDbCollection<T, TModel, TFilter>
    : ObservableCollection<TModel>, IGrowableCollection where T : IDbModel<TModel, TFilter>
{
    private readonly T dbModel;

    public GrowableDbCollection(T dbModel)
    {
        this.dbModel = dbModel;
    }

    public async Task<uint> GrowCollection(uint amount)
    {
        var models = await
            dbModel.LoadMultiple( amount, Convert.ToUInt32(Count));

        var enumerable = models.ToList();
        InsertRange(enumerable);

        return Convert.ToUInt32(enumerable.Count);
    }

    public void InsertRange(IEnumerable<TModel> items)
    {
        CheckReentrancy();

        var startIndex = Count;

        var enumerable = items.ToList();
        foreach (var item in enumerable)
            Items.Add(item);
        
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, enumerable, startIndex));
        OnPropertyChanged(new PropertyChangedEventArgs("Count")); 
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
    }
}