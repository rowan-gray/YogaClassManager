using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.SQLite.Data;

/// <summary>Shared row shape for "which classes did X attend/use" queries - reused by both
/// IPassRepository.GetUsageHistoryAsync (filtered by PassId) and
/// IClassRollRepository.GetAttendanceHistoryAsync (filtered by StudentId), which join the same three
/// tables (ClassStudents, ClassRoll, ClassSchedule) and project to the same record shape.</summary>
internal sealed class ClassAttendanceRow
{
    public int ClassRollId { get; set; }
    public DateOnly Date { get; set; }
    public int ClassScheduleId { get; set; }
    public int Day { get; set; }
    public int Time { get; set; }
    public int IsActive { get; set; }
    public int StudentId { get; set; }
    public int? PassId { get; set; }

    public ClassAttendanceRecord ToRecord()
    {
        var classSchedule = new ClassSchedule(ClassScheduleId, (DayOfWeek)Day,
            TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Time)), isArchived: IsActive == 0);
        return new ClassAttendanceRecord(ClassRollId, Date, classSchedule, StudentId, PassId);
    }
}
