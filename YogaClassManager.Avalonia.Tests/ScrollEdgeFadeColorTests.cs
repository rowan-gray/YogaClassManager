using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Behaviors;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The scroll edge fade blends into the surface the list sits on. It used to be hard-coded
/// black, so in the light theme it showed as a dark smudge over white cards.</summary>
public class ScrollEdgeFadeColorTests
{
    private static ScrollViewer Inside(params Control[] wrappers)
    {
        var scrollViewer = new ScrollViewer { Content = new StackPanel { Height = 2000 } };
        Control current = scrollViewer;
        foreach (var wrapper in wrappers)
        {
            switch (wrapper)
            {
                case Border border: border.Child = current; break;
                case Panel panel: panel.Children.Add(current); break;
            }
            current = wrapper;
        }
        new Window { Content = current, Width = 400, Height = 300 }.Show();
        Dispatcher.UIThread.RunJobs();
        return scrollViewer;
    }

    [AvaloniaFact]
    public void FadesIntoTheNearestOpaqueBackground()
    {
        Assert.Equal(Colors.White, ScrollEdgeShadow.FadeColorFor(Inside(new Border { Background = Brushes.White })));

        var dark = Color.Parse("#FF2B2B2B");
        Assert.Equal(dark, ScrollEdgeShadow.FadeColorFor(Inside(new Border { Background = new SolidColorBrush(dark) })));
    }

    [AvaloniaFact]
    public void SkipsTranslucentBackgrounds_ToTheSurfaceBehindThem()
    {
        var card = Color.Parse("#FFF3F3F3");
        var scrollViewer = Inside(
            new Border { Background = new SolidColorBrush(Color.Parse("#40000000")) }, // translucent tint
            new Border { Background = new SolidColorBrush(card) });

        Assert.Equal(card, ScrollEdgeShadow.FadeColorFor(scrollViewer));
    }

    [AvaloniaFact]
    public void TheRenderedFadeBars_UseTheCardColour_NotBlack()
    {
        var scrollViewer = Inside(new Border { Background = Brushes.White });
        ScrollEdgeShadow.SetIsEnabled(scrollViewer, true);
        scrollViewer.Offset = new global::Avalonia.Vector(0, 200); // something to reveal above and below
        Dispatcher.UIThread.RunJobs();

        var layer = AdornerLayer.GetAdornerLayer(scrollViewer)!;
        var bars = layer.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Background is LinearGradientBrush).ToList();

        Assert.Equal(2, bars.Count);
        Assert.All(bars, bar =>
        {
            var edge = ((LinearGradientBrush)bar.Background!).GradientStops.First().Color;
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), edge);
        });
    }
}
