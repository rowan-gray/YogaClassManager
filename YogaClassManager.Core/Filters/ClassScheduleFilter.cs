using YogaClassManager.Core.Data;

namespace YogaClassManager.Core.Filters;

public struct ClassScheduleFilter
{
    public uint? Id { get; set; }
    public DayOfWeek? Day { get; set; }
    public TimeOnly? TimeFrom { get; set; }
    public TimeOnly? TimeTo { get; set; }
    public bool IncludeArchived { get; set; }
    public KeyValuePair<ClassScheduleSortOptions, Order>? SortBy { get; set; }
}

public enum ClassScheduleSortOptions
{
    [StringValue("ClassScheduleId")] Id,
    [StringValue("Day")] Day,
    [StringValue("Time")] Time
}
