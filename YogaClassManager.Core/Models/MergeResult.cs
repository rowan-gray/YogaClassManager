namespace YogaClassManager.Core.Models;

public record MergeResult(
    int SurvivingIdentityId,
    int RemovedIdentityId,
    int RepointedEmergencyContactLinks,
    int RepointedAttendanceRecords,
    int RepointedPasses);
