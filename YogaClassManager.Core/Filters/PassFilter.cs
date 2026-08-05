using YogaClassManager.Core.Data;

namespace YogaClassManager.Core.Filters;

public struct PassFilter
{
    public uint? Id { get; set; }
    public int? StudentId { get; set; }
    public PassKind Kind { get; set; }
    public bool IncludeExpired { get; set; }
    public bool IncludeDepleted { get; set; }
    public KeyValuePair<PassSortOptions, Order>? SortBy { get; set; }
}

public enum PassKind
{
    Any,
    Dated,
    Casual,
    Term
}

public enum PassSortOptions
{
    [StringValue("PassId")] Id
}
