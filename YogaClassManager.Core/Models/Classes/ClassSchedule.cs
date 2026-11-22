using System.Globalization;
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

    /// <summary>"Monday: 9:00 am" - every list, tile and roll title shows a class this way. The time
    /// is formatted explicitly (12-hour, lowercase am/pm) rather than with the machine's culture, so
    /// it reads the same everywhere and its longest form ("Wednesday: 12:00 pm") is known - the
    /// Dashboard's class tiles are sized to fit that on one line.</summary>
    public override string ToString()
    {
        return $"{Day}: {FormatTime(Time)}";
    }

    /// <summary>12-hour time with lowercase am/pm, e.g. "9:00 am", "12:30 pm".</summary>
    public static string FormatTime(TimeOnly time) =>
        time.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();
}
