using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace YogaClassManager.Avalonia.Behaviors;

/// <summary>
///     Casts a soft drop-shadow over a ScrollViewer's content (or a ListBox's internal
///     PART_ScrollViewer) near whichever edge still has more to reveal, via an AdornerLayer overlay -
///     the shadow bars live above the content in a separate layer (every TopLevel already carries one
///     via its VisualLayerManager), not inside the ScrollViewer's own visual tree, so they never
///     affect its layout, background, or the content's own opacity. See UI_STYLE_GUIDE.md's "Scroll
///     edge shadow" section. Usage: add xmlns:behaviors="using:YogaClassManager.Avalonia.Behaviors" to
///     the view root, then behaviors:ScrollEdgeShadow.IsEnabled="True" on a ScrollViewer or ListBox
///     element.
/// </summary>
public sealed class ScrollEdgeShadow
{
    private ScrollEdgeShadow()
    {
    }

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeShadow, Control, bool>("IsEnabled");

    /// <summary>Shadow height (px) once fully grown, at the edge itself.</summary>
    public static readonly AttachedProperty<double> MaxShadowHeightProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeShadow, Control, double>("MaxShadowHeight", 16);

    /// <summary>How many px of scroll, away from an edge, it takes for that edge's shadow to reach
    /// roughly two-thirds of MaxShadowHeight (see ScrollEdgeShadowMath's 1 - e^-x/k curve).</summary>
    public static readonly AttachedProperty<double> GrowthDistanceProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeShadow, Control, double>("GrowthDistance", 60);

    // Per-ScrollViewer overlay state, stored the same way ScrollEdgeFade stored its computed values -
    // as an attached property on the ScrollViewer itself, so it's automatically scoped to the element
    // and never needs a separate lookup table.
    private static readonly AttachedProperty<ShadowOverlay?> OverlayProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeShadow, ScrollViewer, ShadowOverlay?>("Overlay");

    static ScrollEdgeShadow()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control, bool>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control)
    {
        return control.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        control.SetValue(IsEnabledProperty, value);
    }

    public static double GetMaxShadowHeight(Control control)
    {
        return control.GetValue(MaxShadowHeightProperty);
    }

    public static void SetMaxShadowHeight(Control control, double value)
    {
        control.SetValue(MaxShadowHeightProperty, value);
    }

    public static double GetGrowthDistance(Control control)
    {
        return control.GetValue(GrowthDistanceProperty);
    }

    public static void SetGrowthDistance(Control control, double value)
    {
        control.SetValue(GrowthDistanceProperty, value);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs<bool> e)
    {
        var enabled = e.NewValue.GetValueOrDefault();

        switch (control)
        {
            case ScrollViewer scrollViewer:
                if (enabled)
                    Attach(scrollViewer);
                else
                    Detach(scrollViewer);
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
        if (e.NameScope.Find<ScrollViewer>("PART_ScrollViewer") is { } scrollViewer)
            Attach(scrollViewer);
    }

    private static void Attach(ScrollViewer scrollViewer)
    {
        if (scrollViewer.GetValue(OverlayProperty) is not null)
            return;

        scrollViewer.SetValue(OverlayProperty, new ShadowOverlay(scrollViewer));
    }

    private static void Detach(ScrollViewer scrollViewer)
    {
        if (scrollViewer.GetValue(OverlayProperty) is { } overlay)
        {
            overlay.Dispose();
            scrollViewer.ClearValue(OverlayProperty);
        }
    }

    /// <summary>The colour of the surface <paramref name="visual" /> sits on: the first fully opaque
    /// solid background among it and its ancestors (Border, Panel, TemplatedControl - Cards, the page,
    /// the window), else the theme's surface colour (SystemAltHighColor). Translucent and gradient
    /// backgrounds are skipped, since a fade can only match a solid colour.</summary>
    public static Color FadeColorFor(Visual visual)
    {
        foreach (var candidate in visual.GetSelfAndVisualAncestors())
        {
            var background = candidate switch
            {
                Border border => border.Background,
                Panel panel => panel.Background,
                TemplatedControl templated => templated.Background,
                _ => null
            };

            if (background is ISolidColorBrush { Opacity: >= 1 } solid && solid.Color.A == 255)
                return solid.Color;
        }

        var theme = (visual as StyledElement)?.ActualThemeVariant;
        return Application.Current?.TryGetResource("SystemAltHighColor", theme, out var surface) == true &&
               surface is Color color
            ? color
            : Colors.White;
    }

    /// <summary>Owns the two shadow-bar Borders for one ScrollViewer: creates them, keeps them mounted
    /// in whichever AdornerLayer the ScrollViewer is under (re-attaching if it moves to a new visual
    /// root), sizes them to match the ScrollViewer's own bounds, and updates their heights on every
    /// scroll change.</summary>
    private sealed class ShadowOverlay : IDisposable
    {
        private readonly ScrollViewer scrollViewer;
        private readonly Border topShadow;
        private readonly Border bottomShadow;
        private readonly Panel root;
        private bool isInLayer;

        public ShadowOverlay(ScrollViewer scrollViewer)
        {
            this.scrollViewer = scrollViewer;

            topShadow = CreateShadowBar(VerticalAlignment.Top);
            bottomShadow = CreateShadowBar(VerticalAlignment.Bottom);

            root = new Panel { IsHitTestVisible = false };
            root.Children.Add(topShadow);
            root.Children.Add(bottomShadow);

            scrollViewer.ScrollChanged += OnScrollChanged;
            scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
            scrollViewer.AttachedToVisualTree += OnAttachedToVisualTree;
            scrollViewer.DetachedFromVisualTree += OnDetachedFromVisualTree;
            scrollViewer.ActualThemeVariantChanged += OnThemeChanged;

            if (scrollViewer.GetVisualRoot() is not null)
                AttachToLayer();

            ApplyFadeColor();

            UpdateSize();
            UpdateShadows();
        }

        // A plain 2-stop (opaque -> transparent) gradient falls off linearly, which reads as a hard
        // edge with a long faint tail rather than a soft shadow. Sampling an ease-out curve
        // (alpha = (1-t)^2) at several intermediate stops instead spends the same 0-to-max alpha
        // range mostly right at the edge, tapering smoothly rather than trailing off evenly.
        private const int ShadowGradientStopCount = 7;

        private static Border CreateShadowBar(VerticalAlignment alignment)
        {
            return new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = alignment,
                Height = 0,
                IsHitTestVisible = false
            };
        }

        /// <summary>The fade's colour at full strength (at the edge) through to transparent, eased.</summary>
        private static LinearGradientBrush CreateFadeBrush(Color color, bool isTop)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, isTop ? 0 : 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, isTop ? 1 : 0, RelativeUnit.Relative)
            };

            for (var i = 0; i < ShadowGradientStopCount; i++)
            {
                var t = i / (double)(ShadowGradientStopCount - 1);
                var alpha = (byte)(Math.Pow(1 - t, 2) * 0xFF);
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, color.R, color.G, color.B), t));
            }

            return brush;
        }

        /// <summary>
        ///     The edges fade into whatever the list sits on. This used to be hard-coded black, which read
        ///     as a dark smudge over light-theme cards. Resolved from the nearest opaque solid background
        ///     above the ScrollViewer (a Card, the page, the window), re-resolved whenever the theme
        ///     changes; falls back to the theme's own surface colour.
        /// </summary>
        private void ApplyFadeColor()
        {
            var color = FadeColorFor(scrollViewer);
            topShadow.Background = CreateFadeBrush(color, isTop: true);
            bottomShadow.Background = CreateFadeBrush(color, isTop: false);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            // Posted so theme-scoped DynamicResource backgrounds have already switched when we look.
            Dispatcher.UIThread.Post(ApplyFadeColor, DispatcherPriority.Background);
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            AttachToLayer();
            ApplyFadeColor();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            DetachFromLayer();
        }

        private void AttachToLayer()
        {
            if (isInLayer)
                return;
            if (AdornerLayer.GetAdornerLayer(scrollViewer) is null)
                return;

            AdornerLayer.SetAdorner(scrollViewer, root);
            isInLayer = true;
            UpdateSize();
        }

        private void DetachFromLayer()
        {
            if (!isInLayer)
                return;

            AdornerLayer.GetAdornerLayer(scrollViewer)?.Children.Remove(root);
            isInLayer = false;
        }

        private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.BoundsProperty)
                UpdateSize();
        }

        private void UpdateSize()
        {
            root.Width = scrollViewer.Bounds.Width;
            root.Height = scrollViewer.Bounds.Height;
        }

        private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            UpdateShadows();
        }

        private void UpdateShadows()
        {
            var state = ScrollEdgeShadowMath.Compute(scrollViewer.Extent.Height, scrollViewer.Viewport.Height,
                scrollViewer.Offset.Y, GetMaxShadowHeight(scrollViewer), GetGrowthDistance(scrollViewer));

            topShadow.Height = state.TopHeight;
            bottomShadow.Height = state.BottomHeight;
        }

        public void Dispose()
        {
            scrollViewer.ScrollChanged -= OnScrollChanged;
            scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
            scrollViewer.AttachedToVisualTree -= OnAttachedToVisualTree;
            scrollViewer.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            scrollViewer.ActualThemeVariantChanged -= OnThemeChanged;
            DetachFromLayer();
        }
    }
}

/// <summary>
///     The pure scroll-position math behind ScrollEdgeShadow, split out so it's unit-testable without
///     a live ScrollViewer. Each edge's shadow height depends only on how far the current offset has
///     moved away from THAT edge - 0px flush at the edge, easing up towards maxHeight the further away
///     scrolling gets - independent of whether the opposite edge still has content.
/// </summary>
public static class ScrollEdgeShadowMath
{
    public static ScrollEdgeShadowState Compute(double extent, double viewport, double offset, double maxHeight,
        double growthDistance)
    {
        if (viewport <= 0 || growthDistance <= 0)
            return new ScrollEdgeShadowState(0, 0);

        var topDistance = Math.Max(0, offset);
        var bottomDistance = Math.Max(0, extent - viewport - offset);

        return new ScrollEdgeShadowState(
            Grow(topDistance, maxHeight, growthDistance),
            Grow(bottomDistance, maxHeight, growthDistance));
    }

    // 1 - e^(-distance/growthDistance): 0 at distance 0, easing towards (never quite reaching, but
    // visually indistinguishable from) maxHeight as distance grows - the standard "exponential
    // approach to a ceiling" curve, so there's no seam where a linear ramp would otherwise hit a hard
    // cap.
    private static double Grow(double distance, double maxHeight, double growthDistance)
    {
        return maxHeight * (1 - Math.Exp(-distance / growthDistance));
    }
}

public readonly record struct ScrollEdgeShadowState(double TopHeight, double BottomHeight);
