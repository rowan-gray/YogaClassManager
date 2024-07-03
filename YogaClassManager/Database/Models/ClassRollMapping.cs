using YogaClassManager.Models.Classes;

namespace YogaClassManager.Database.Models;

internal class ClassRollMapping
{
    public int ClassId { get; set; }
    public int ClassScheduleId { get; set; }
    public string Date { get; set; }

    public ClassRoll ToClassRoll()
    {
        return new ClassRoll(ClassId, StringToDateOnly(Date),
            new ClassSchedule(ClassScheduleId, DayOfWeek.Monday, TimeOnly.MaxValue, true), null);
    }

    protected DateOnly StringToDateOnly(string date)
    {
        return DateOnly.ParseExact(date, "yyyy-MM-dd");
    }
}