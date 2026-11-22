using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Core.SQLite.Data;

/// <summary>Row shape for IPassRepository.GetUsageHistoryAsync's "which classes used this pass" query
/// (filtered by PassId) - joins ClassStudents/ClassRoll/ClassSchedule and projects to
/// ClassAttendanceRecord. IClassRollRepository.GetAttendanceHistoryAsync no longer uses this: it goes
/// through Query/SqliteClassRollDbModel instead, so its filtering/ordering can't drift from the Class
/// Rolls list's.</summary>
internal sealed class ClassAttendanceRow
{
    public int ClassRollId { get; set; }
    public DateOnly Date { get; set; }
    public int ClassScheduleId { get; set; }
    public int Day { get; set; }
    public int Time { get; set; }
    public int IsArchived { get; set; }
    public int StudentId { get; set; }

    /// <summary>Every row from GetUsageHistoryAsync is a use of the same, already-known pass, so the
    /// caller passes it in rather than this row resolving it itself.</summary>
    public ClassAttendanceRecord ToRecord(Pass? pass)
    {
        var classSchedule = new ClassSchedule(ClassScheduleId, (DayOfWeek)Day,
            TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Time)), isArchived: IsArchived != 0);
        return new ClassAttendanceRecord(ClassRollId, Date, classSchedule, StudentId, pass);
    }
}
