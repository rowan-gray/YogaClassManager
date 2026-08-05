using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Models.Classes;

public class ClassRoll : IIdentifiable
{
    public ClassRoll(int id, DateOnly date, ClassSchedule classSchedule, List<ClassRollEntry> studentEntries)
    {
        Id = id;
        Date = date;
        ClassSchedule = classSchedule;
        StudentEntries = studentEntries;
    }

    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public ClassSchedule ClassSchedule { get; set; }
    public List<ClassRollEntry> StudentEntries { get; set; }

    public override string ToString()
    {
        return $"{ClassSchedule} ({Date})";
    }
}
