using System.Collections;

namespace YogaClassManager.Core.Data;

/// <summary>
///     A collection that can lazily load more of its backing data on demand, driving
///     infinite-scroll-style UI (see YogaClassManager.Avalonia's GrowableItemsControl).
/// </summary>
public interface IGrowableCollection : IEnumerable
{
    Task<uint> GrowCollection(uint amount);
}
