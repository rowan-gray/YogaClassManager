using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace YogaClassManager.Avalonia.Behaviors;

/// <summary>
///     Fades a ScrollViewer's content (or a ListBox's internal PART_ScrollViewer) near whichever edge
///     still has more to reveal, via OpacityMask - no overlay markup, no background color to match
///     per call site, and no IsHitTestVisible guard needed (opacity never gates hit-testing). See
///     UI_STYLE_GUIDE.md's "Scroll edge fade" section. Usage: add
///     xmlns:behaviors="using:YogaClassManager.Avalonia.Behaviors" to the view root, then
///     behaviors:ScrollEdgeFade.IsEnabled="True" on a ScrollViewer or ListBox element.
/// </summary>
public sealed class ScrollEdgeFade
{
    private ScrollEdgeFade()
    {
    }

    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(200);

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeFade, Control, bool>("IsEnabled");

    public static readonly AttachedProperty<double> FadeHeightProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeFade, Control, double>("FadeHeight", 24);

    // Animated 0-1 "how faded is this edge right now" values. A Transitions entry is attached to
    // each (see EnsureFadeTransitions) so the fade itself eases in/out instead of jump-cutting the
    // instant the scroll position crosses the can-scroll threshold; every intermediate frame of that
    // animation raises a Changed notification (Avalonia's Transitions animate the real property
    // value, the same mechanism that already drives the app's Button/ListBoxItem hover transitions),
    // which RebuildBrush below uses to keep the OpacityMask in sync with the animation.
    private static readonly AttachedProperty<double> TopFadeOpacityProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeFade, ScrollViewer, double>("TopFadeOpacity");

    private static readonly AttachedProperty<double> BottomFadeOpacityProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeFade, ScrollViewer, double>("BottomFadeOpacity");

    private static readonly AttachedProperty<double> TopStopOffsetProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeFade, ScrollViewer, double>("TopStopOffset");

    private static readonly AttachedProperty<double> BottomStopOffsetProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeFade, ScrollViewer, double>("BottomStopOffset", 1);

    static ScrollEdgeFade()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control, bool>(OnIsEnabledChanged);
        TopFadeOpacityProperty.Changed.AddClassHandler<ScrollViewer, double>((sv, e) => RebuildBrush(sv));
        BottomFadeOpacityProperty.Changed.AddClassHandler<ScrollViewer, double>((sv, e) => RebuildBrush(sv));
    }

    public static bool GetIsEnabled(Control control)
    {
        return control.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        control.SetValue(IsEnabledProperty, value);
    }

    public static double GetFadeHeight(Control control)
    {
        return control.GetValue(FadeHeightProperty);
    }

    public static void SetFadeHeight(Control control, double value)
    {
        control.SetValue(FadeHeightProperty, value);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs<bool> e)
    {
        var enabled = e.NewValue.GetValueOrDefault();

        switch (control)
        {
            case ScrollViewer scrollViewer:
                scrollViewer.ScrollChanged -= OnScrollChanged;
                if (enabled)
                {
                    scrollViewer.ScrollChanged += OnScrollChanged;
                    UpdateFadeState(scrollViewer);
                }
                else
                {
                    scrollViewer.OpacityMask = null;
                }

                break;

            // ListBox (and anything else whose Fluent template exposes a named PART_ScrollViewer)
            // doesn't have a ScrollViewer to attach to until its template is applied.
            case TemplatedControl templatedControl:
                templatedControl.TemplateApplied -= OnTemplateApplied;
                if (enabled)
                    templatedControl.TemplateApplied += OnTemplateApplied;
                break;
        }
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<ScrollViewer>("PART_ScrollViewer") is not { } scrollViewer)
            return;

        scrollViewer.ScrollChanged -= OnScrollChanged;
        scrollViewer.ScrollChanged += OnScrollChanged;
        UpdateFadeState(scrollViewer);
    }

    private static void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
            UpdateFadeState(scrollViewer);
    }

    private static void UpdateFadeState(ScrollViewer scrollViewer)
    {
        EnsureFadeTransitions(scrollViewer);

        var fadeHeight = GetFadeHeight(scrollViewer);
        var state = ScrollEdgeFadeMath.Compute(scrollViewer.Extent.Height, scrollViewer.Viewport.Height,
            scrollViewer.Offset.Y, fadeHeight);

        // Position updates immediately (it only changes on resize, no need to animate it); the
        // opacity targets below are what actually animate, via the Transitions attached above.
        scrollViewer.SetValue(TopStopOffsetProperty, state.TopStopOffset);
        scrollViewer.SetValue(BottomStopOffsetProperty, state.BottomStopOffset);
        scrollViewer.SetValue(TopFadeOpacityProperty, state.CanScrollUp ? 1d : 0d);
        scrollViewer.SetValue(BottomFadeOpacityProperty, state.CanScrollDown ? 1d : 0d);

        RebuildBrush(scrollViewer);
    }

    private static void EnsureFadeTransitions(ScrollViewer scrollViewer)
    {
        if (scrollViewer.Transitions is not null)
            return;

        scrollViewer.Transitions = new Transitions
        {
            new DoubleTransition { Property = TopFadeOpacityProperty, Duration = FadeDuration },
            new DoubleTransition { Property = BottomFadeOpacityProperty, Duration = FadeDuration }
        };
    }

    private static void RebuildBrush(ScrollViewer scrollViewer)
    {
        var topOpacity = scrollViewer.GetValue(TopFadeOpacityProperty);
        var bottomOpacity = scrollViewer.GetValue(BottomFadeOpacityProperty);

        if (topOpacity <= 0 && bottomOpacity <= 0)
        {
            scrollViewer.OpacityMask = null;
            return;
        }

        var topStop = scrollViewer.GetValue(TopStopOffsetProperty);
        var bottomStop = scrollViewer.GetValue(BottomStopOffsetProperty);
        var topAlpha = (byte)(255 - Math.Clamp(topOpacity, 0, 1) * 255);
        var bottomAlpha = (byte)(255 - Math.Clamp(bottomOpacity, 0, 1) * 255);

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(topAlpha, 0, 0, 0), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), topStop));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), bottomStop));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(bottomAlpha, 0, 0, 0), 1));

        scrollViewer.OpacityMask = brush;
    }
}

/// <summary>
///     The pure scroll-position math behind ScrollEdgeFade, split out so it's unit-testable without a
///     live ScrollViewer.
/// </summary>
public static class ScrollEdgeFadeMath
{
    public static ScrollEdgeFadeState Compute(double extent, double viewport, double offset, double fadeHeight)
    {
        if (viewport <= 0)
            return new ScrollEdgeFadeState(false, false, 0, 1);

        var canScrollUp = offset > 0.5;
        var canScrollDown = offset < extent - viewport - 0.5;
        var fraction = Math.Min(fadeHeight / viewport, 0.5);

        return new ScrollEdgeFadeState(canScrollUp, canScrollDown, fraction, 1 - fraction);
    }
}

public readonly record struct ScrollEdgeFadeState(bool CanScrollUp, bool CanScrollDown, double TopStopOffset,
    double BottomStopOffset);
