using YogaClassManager.Core.Data;

namespace YogaClassManager.Core.Filters;

public struct IdentityFilter
{
    public uint? Id { get; set; }

    /// <summary>Matches full name (contains), first name, or last name (either startswith) - same
    /// semantics as StudentFilter.NameFilter, for a single search-box field. Prefer this over
    /// FirstNameFilter/LastNameFilter for a name search box; those two remain for callers that
    /// specifically need one field or the other.</summary>
    public string? NameFilter { get; set; }

    public string? FirstNameFilter { get; set; }
    public string? LastNameFilter { get; set; }
    public string? EmailFilter { get; set; }
    public string? PhoneNumberFilter { get; set; }
    public bool? IsActive { get; set; }
    public KeyValuePair<IdentitySortOptions, Order>? SortBy { get; set; }
}

public enum IdentitySortOptions
{
    [StringValue("PersonId")] Id,
    [StringValue("FirstName")] FirstName,
    [StringValue("LastName")] LastName,
    [StringValue("PhoneNumber")] PhoneNumber,
    [StringValue("Email")] Email
}
