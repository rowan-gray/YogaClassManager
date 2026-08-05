namespace YogaClassManager.Core.Models;

public record IdentityLinkageSummary(
    bool IsStudent,
    int EmergencyContactLinkCount,
    int PassCount,
    int AttendanceCount)
{
    public bool HasAnyLinks => IsStudent || EmergencyContactLinkCount > 0 || PassCount > 0 || AttendanceCount > 0;
}
