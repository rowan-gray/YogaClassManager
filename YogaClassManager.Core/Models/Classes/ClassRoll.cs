using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Models.Classes;

public class ClassRoll : ObservableObject, IIdentifiable
{
    private List<ClassRollEntry> studentEntries;

    public ClassRoll(int id, DateOnly date, ClassSchedule classSchedule, List<ClassRollEntry> studentEntries)
    {
        Id = id;
        Date = date;
        ClassSchedule = classSchedule;
        this.studentEntries = studentEntries;
    }

    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public ClassSchedule ClassSchedule { get; set; }

    /// <summary>A plain List, so adding to it in place notifies nothing - replace it to update anything
    /// bound to it (e.g. a list row's attendee count).</summary>
    public List<ClassRollEntry> StudentEntries
    {
        get => studentEntries;
        set => SetProperty(ref studentEntries, value);
    }

    public override string ToString()
    {
        return $"{ClassSchedule} ({Date})";
    }
}
