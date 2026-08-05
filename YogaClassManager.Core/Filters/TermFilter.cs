using YogaClassManager.Core.Data;

namespace YogaClassManager.Core.Filters;

public struct TermFilter
{
    public uint? Id { get; set; }
    public string? NameFilter { get; set; }
    public bool IncludeCompleted { get; set; }
    public KeyValuePair<TermSortOptions, Order>? SortBy { get; set; }
}

public enum TermSortOptions
{
    [StringValue("TermId")] Id,
    [StringValue("Name")] Name,
    [StringValue("StartDate")] StartDate,
    [StringValue("EndDate")] EndDate
}
