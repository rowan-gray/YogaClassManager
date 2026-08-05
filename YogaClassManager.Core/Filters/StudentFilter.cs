using YogaClassManager.Core.Data;

namespace YogaClassManager.Core.Filters;

public struct StudentFilter
{
    public uint? Id { get; set; }
    public string? NameFilter { get; set; }
    public bool? IsActive { get; set; }
    public IReadOnlyCollection<int>? ExcludeIds { get; set; }

    /// <summary>Inclusive lower/upper bounds on a student's most recent class attendance date.
    /// A student who has never attended is excluded whenever either bound is set.</summary>
    public DateOnly? LastAttendedFrom { get; set; }
    public DateOnly? LastAttendedTo { get; set; }

    public KeyValuePair<StudentSortOptions, Order>? SortBy { get; set; }
}

public enum StudentSortOptions
{
    [StringValue("PersonId")] Id,
    [StringValue("FirstName")] FirstName,
    [StringValue("LastName")] LastName,
    [StringValue("LastAttendance")] LastAttendance
}
