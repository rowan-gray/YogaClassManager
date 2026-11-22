using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Transitions;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     The selection used to flash grey on its way to the selection colour: Avalonia's BrushTransition
///     blends straight (non-premultiplied) RGBA, and the translucent white/black hover/pressed tints
///     drag the colour through white/black mid-way. PremultipliedBrushTransition blends premultiplied
///     colour instead - a true cross-fade of what's rendered - so no in-between frame can land outside
///     the two end colours.
/// </summary>
public class PremultipliedBrushTransitionTests
{
    // Dark theme: pressed tint (SystemListMediumColor) -> selection (SystemAccentColorDark2), over the
    // dark card behind a list row.
    private static readonly Color PressedTint = Color.Parse("#33FFFFFF");
    private static readonly Color Selection = Color.Parse("#FF004175");
    private static readonly Color DarkCard = Color.Parse("#FF2B2B2B");

    private static (double R, double G, double B) Over(Color top, Color background)
    {
        var a = top.A / 255.0;
        return (top.R * a + background.R * (1 - a), top.G * a + background.G * (1 - a),
            top.B * a + background.B * (1 - a));
    }

    private static Color StraightBlend(Color from, Color to, double t) => Color.FromArgb(
        (byte)Math.Round(from.A + (to.A - from.A) * t), (byte)Math.Round(from.R + (to.R - from.R) * t),
        (byte)Math.Round(from.G + (to.G - from.G) * t), (byte)Math.Round(from.B + (to.B - from.B) * t));

    /// <summary>Is every channel of <paramref name="mid" /> within the range spanned by the two
    /// rendered end colours (allowing one step of rounding)?</summary>
    private static bool Between((double R, double G, double B) mid, (double R, double G, double B) a,
        (double R, double G, double B) b)
    {
        static bool In(double v, double x, double y) => v >= Math.Min(x, y) - 1 && v <= Math.Max(x, y) + 1;
        return In(mid.R, a.R, b.R) && In(mid.G, a.G, b.G) && In(mid.B, a.B, b.B);
    }

    [Fact]
    public void EveryFrame_RendersBetweenTheTwoEnds_WhereAStraightBlendFlashes()
    {
        var start = Over(PressedTint, DarkCard);
        var end = Over(Selection, DarkCard);

        // The bug, demonstrated: the straight blend's midpoint renders outside both ends (lighter).
        Assert.False(Between(Over(StraightBlend(PressedTint, Selection, 0.5), DarkCard), start, end));

        for (var t = 0.0; t <= 1.0; t += 0.05)
        {
            var frame = Over(PremultipliedBrushTransition.Blend(PressedTint, Selection, t), DarkCard);
            Assert.True(Between(frame, start, end), $"frame at {t:F2} renders outside its ends");
        }
    }

    [Fact]
    public void Blend_HitsBothEndsExactly()
    {
        Assert.Equal(PressedTint, PremultipliedBrushTransition.Blend(PressedTint, Selection, 0));
        Assert.Equal(Selection, PremultipliedBrushTransition.Blend(PressedTint, Selection, 1));
    }

    /// <summary>Guards against a plain BrushTransition creeping back onto a Background.</summary>
    [AvaloniaFact]
    public void ButtonsAndListRows_AnimateTheirBackgroundWithThePremultipliedTransition()
    {
        var button = new Button { Content = "Add" };
        var row = new ListBoxItem { Content = "Ada Lovelace" };
        new Window { Content = new StackPanel { Children = { button, new ListBox { ItemsSource = new[] { row } } } } }
            .Show();
        Dispatcher.UIThread.RunJobs();

        foreach (var control in new Control[] { button, row })
        {
            var presenter = control.GetVisualDescendants().OfType<ContentPresenter>().First();
            var transitions = presenter.Transitions ?? [];

            Assert.Contains(transitions, t => t is PremultipliedBrushTransition);
            Assert.DoesNotContain(transitions, t => t is BrushTransition);
        }
    }
}
