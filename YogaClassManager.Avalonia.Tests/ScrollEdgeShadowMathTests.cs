using YogaClassManager.Avalonia.Behaviors;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Covers the pure scroll-position math behind ScrollEdgeShadow in isolation from any live
///     ScrollViewer - see UI_STYLE_GUIDE.md's "Scroll edge shadow" section for what this drives.
/// </summary>
public class ScrollEdgeShadowMathTests
{
    [Fact]
    public void Compute_ContentFitsEntirely_NoShadowEitherEdge()
    {
        var state = ScrollEdgeShadowMath.Compute(extent: 150, viewport: 200, offset: 0, maxHeight: 16,
            growthDistance: 60);

        Assert.Equal(0, state.TopHeight);
        Assert.Equal(0, state.BottomHeight);
    }

    [Fact]
    public void Compute_ScrolledToTop_TopHeightIsZero()
    {
        var state = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 200, offset: 0, maxHeight: 16,
            growthDistance: 60);

        Assert.Equal(0, state.TopHeight);
        Assert.True(state.BottomHeight > 0);
    }

    [Fact]
    public void Compute_ScrolledToBottom_BottomHeightIsZero()
    {
        var state = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 200, offset: 800, maxHeight: 16,
            growthDistance: 60);

        Assert.True(state.TopHeight > 0);
        Assert.Equal(0, state.BottomHeight);
    }

    [Fact]
    public void Compute_ScrolledInMiddle_BothEdgesHaveShadow()
    {
        var state = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 200, offset: 400, maxHeight: 16,
            growthDistance: 60);

        Assert.True(state.TopHeight > 0);
        Assert.True(state.BottomHeight > 0);
    }

    [Fact]
    public void Compute_TopDistanceEqualsGrowthDistance_ReachesOneMinusOneOverEFraction()
    {
        // 1 - e^-1 =~ 0.632 of maxHeight once you've scrolled exactly one growthDistance away from
        // the edge - the defining point of the exponential-approach curve.
        var state = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 200, offset: 60, maxHeight: 16,
            growthDistance: 60);

        Assert.Equal(16 * (1 - Math.Exp(-1)), state.TopHeight, precision: 6);
    }

    [Fact]
    public void Compute_FarPastGrowthDistance_ApproachesMaxHeightWithoutExceedingIt()
    {
        var state = ScrollEdgeShadowMath.Compute(extent: 10000, viewport: 200, offset: 600, maxHeight: 16,
            growthDistance: 60);

        Assert.True(state.TopHeight < 16);
        Assert.True(state.TopHeight > 15.99);
    }

    [Fact]
    public void Compute_EqualSteps_ProduceShrinkingIncrements()
    {
        // The defining shape of the exponential-approach curve (as opposed to a straight linear ramp
        // to the cap): each further equal step away from the edge adds less height than the step
        // before it, since growth eases off the closer it gets to maxHeight.
        var atZero = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 200, offset: 0, maxHeight: 16,
            growthDistance: 60);
        var atThirty = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 200, offset: 30, maxHeight: 16,
            growthDistance: 60);
        var atSixty = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 200, offset: 60, maxHeight: 16,
            growthDistance: 60);

        var firstStep = atThirty.TopHeight - atZero.TopHeight;
        var secondStep = atSixty.TopHeight - atThirty.TopHeight;

        Assert.True(secondStep < firstStep);
    }

    [Fact]
    public void Compute_ZeroViewport_ReturnsNoShadow()
    {
        var state = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 0, offset: 0, maxHeight: 16,
            growthDistance: 60);

        Assert.Equal(0, state.TopHeight);
        Assert.Equal(0, state.BottomHeight);
    }

    [Fact]
    public void Compute_ZeroGrowthDistance_ReturnsNoShadow()
    {
        var state = ScrollEdgeShadowMath.Compute(extent: 1000, viewport: 200, offset: 400, maxHeight: 16,
            growthDistance: 0);

        Assert.Equal(0, state.TopHeight);
        Assert.Equal(0, state.BottomHeight);
    }
}
