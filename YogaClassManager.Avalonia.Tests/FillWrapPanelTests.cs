using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using YogaClassManager.Avalonia.Controls;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>FillWrapPanel packs children by their WrapWidth, but a child alone on a row must still
/// shrink to the available width. With MinWidth doing both jobs, the Student page's Emergency contacts /
/// Health concerns panels stayed 400px wide in a narrower single column and overflowed.</summary>
public class FillWrapPanelTests
{
    private static (Border First, Border Second) Layout(double panelWidth)
    {
        var first = new Border { Height = 50 };
        var second = new Border { Height = 50 };
        FillWrapPanel.SetWrapWidth(first, 400);
        FillWrapPanel.SetWrapWidth(second, 400);
        var panel = new FillWrapPanel { Spacing = 16, Width = panelWidth, Children = { first, second } };
        new Window { Content = panel, Width = 1200, Height = 400 }.Show();
        Dispatcher.UIThread.RunJobs();
        return (first, second);
    }

    [AvaloniaFact]
    public void InANarrowColumn_EachChildTakesItsOwnRow_AndShrinksToFit()
    {
        var (first, second) = Layout(panelWidth: 300);

        Assert.Equal(300, first.Bounds.Width, precision: 1);   // not 400
        Assert.Equal(300, second.Bounds.Width, precision: 1);
        Assert.True(second.Bounds.Top > first.Bounds.Top);     // stacked
    }

    [AvaloniaFact]
    public void WithRoom_ChildrenShareARow_FillingItEvenly()
    {
        var (first, second) = Layout(panelWidth: 1000);

        Assert.Equal(first.Bounds.Top, second.Bounds.Top);
        Assert.Equal((1000 - 16) / 2.0, first.Bounds.Width, precision: 1);
        Assert.Equal(first.Bounds.Width, second.Bounds.Width, precision: 1);
    }
}
