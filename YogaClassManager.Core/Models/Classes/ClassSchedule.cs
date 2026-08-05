using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.Core.Models.Classes;

public partial class ClassSchedule : ObservableObject, IIdentifiable, IUpdateable<ClassSchedule>, IEquatable<ClassSchedule>
{
    [ObservableProperty] private int id;

    [ObservableProperty] private DayOfWeek day;

    [ObservableProperty] private TimeOnly time;

    [ObservableProperty] private bool isArchived;

    public ClassSchedule(int id, DayOfWeek day, TimeOnly time, bool isArchived)
    {
        Id = id;
        Day = day;
        Time = time;
        IsArchived = isArchived;
    }

    public void Update(ClassSchedule updatedData)
    {
        Day = updatedData.Day;
        Time = updatedData.Time;
        IsArchived = updatedData.IsArchived;
    }

    public static ClassSchedule Copy(ClassSchedule classSchedule)
    {
        return new ClassSchedule(classSchedule.Id, classSchedule.Day, classSchedule.Time, classSchedule.IsArchived);
    }

    public bool Equals(ClassSchedule? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id && Day == other.Day && Time == other.Time && IsArchived == other.IsArchived;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ClassSchedule);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Day, Time, IsArchived);
    }

    public override string ToString()
    {
        return $"{Day}: {Time}";
    }
}
