using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Core.Models.Classes;

public record ClassAttendanceRecord(
    int ClassRollId,
    DateOnly Date,
    ClassSchedule ClassSchedule,
    int StudentId,
    Pass? Pass);
