using YogaClassManager.Core.Data;

namespace YogaClassManager.Core.Filters;

public struct ClassRollFilter
{
    public uint? Id { get; set; }
    public int? ClassScheduleId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public TimeOnly? TimeFrom { get; set; }
    public TimeOnly? TimeTo { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public KeyValuePair<ClassRollSortOptions, Order>? SortBy { get; set; }
}

public enum ClassRollSortOptions
{
    [StringValue("Date")] Date,
    [StringValue("Time")] Time,
    [StringValue("DayOfWeek")] DayOfWeek
}
