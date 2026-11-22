using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using YogaClassManager.Avalonia.Controls;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The "..." menus and sort/filter popups fade in and out: a close request holds the flyout
/// open while it fades, then really closes it.</summary>
public class FadeFlyoutTests
{
    private static void PumpFor(int milliseconds)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static (FadeMenuFlyout Flyout, Button Target) Show()
    {
        var target = new Button { Content = "..." };
        new Window { Content = target, Width = 400, Height = 300 }.Show();
        Dispatcher.UIThread.RunJobs();

        var flyout = new FadeMenuFlyout { Items = { new MenuItem { Header = "Edit" } } };
        flyout.ShowAt(target);
        Dispatcher.UIThread.RunJobs();
        return (flyout, target);
    }

    [AvaloniaFact]
    public void Closing_WaitsForTheFadeOut_ThenCloses()
    {
        var (flyout, _) = Show();
        Assert.True(flyout.IsOpen);

        flyout.Hide();
        Dispatcher.UIThread.RunJobs();
        Assert.True(flyout.IsOpen); // still showing while it fades

        PumpFor(300);
        Assert.False(flyout.IsOpen);
    }

    [AvaloniaFact]
    public void ItCanBeOpenedAndClosedAgain()
    {
        var (flyout, target) = Show();
        flyout.Hide();
        PumpFor(300);
        Assert.False(flyout.IsOpen);

        flyout.ShowAt(target);
        Dispatcher.UIThread.RunJobs();
        Assert.True(flyout.IsOpen);

        flyout.Hide();
        PumpFor(300);
        Assert.False(flyout.IsOpen);
    }
}
