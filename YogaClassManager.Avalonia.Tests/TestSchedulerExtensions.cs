using Microsoft.Reactive.Testing;

namespace YogaClassManager.Avalonia.Tests;

internal static class TestSchedulerExtensions
{
    public static void AdvanceByMs(this TestScheduler scheduler, double milliseconds)
    {
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(milliseconds).Ticks);
    }
}
