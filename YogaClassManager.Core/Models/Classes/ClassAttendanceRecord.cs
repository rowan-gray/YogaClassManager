namespace YogaClassManager.Core.Models.Classes;

public record ClassAttendanceRecord(
    int ClassRollId,
    DateOnly Date,
    ClassSchedule ClassSchedule,
    int StudentId,
    int? PassId);
