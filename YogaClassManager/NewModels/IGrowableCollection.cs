using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace YogaClassManager.NewModels;

public interface IGrowableCollection : IEnumerable
{
    Task<uint> GrowCollection(uint amount);
}