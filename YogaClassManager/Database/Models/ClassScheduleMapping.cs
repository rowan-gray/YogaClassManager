using YogaClassManager.Helpers;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Database.Models;

internal class ClassScheduleMapping
{
    public int ClassScheduleId { get; set; }
    public int Day { get; set; }
    public int Time { get; set; }
    public bool IsActive { get; set; }

    public ClassSchedule ToClassSchedule()
    {
        return new ClassSchedule(ClassScheduleId, (DayOfWeek)Day, TimeOnlyHelpers.GetTimeOnlyFromMinutes(Time),
            IsActive);
    }
}