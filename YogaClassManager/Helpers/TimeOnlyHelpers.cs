namespace YogaClassManager.Helpers;

internal static class TimeOnlyHelpers
{
    internal static TimeOnly GetTimeOnlyFromMinutes(int minutes)
    {
        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes));
    }
}