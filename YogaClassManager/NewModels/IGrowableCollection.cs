using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace YogaClassManager.NewModels;

public interface IGrowableCollection : ICollection, INotifyCollectionChanged, INotifyPropertyChanged
{
    Task GrowCollection(uint amount);
}