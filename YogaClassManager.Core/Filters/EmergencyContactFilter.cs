using YogaClassManager.Core.Data;

namespace YogaClassManager.Core.Filters;

public struct EmergencyContactFilter
{
    public uint? Id { get; set; }
    public int? StudentId { get; set; }
    public KeyValuePair<EmergencyContactSortOptions, Order>? SortBy { get; set; }
}

public enum EmergencyContactSortOptions
{
    [StringValue("PersonId")] Id,
    [StringValue("FirstName")] FirstName,
    [StringValue("LastName")] LastName
}
