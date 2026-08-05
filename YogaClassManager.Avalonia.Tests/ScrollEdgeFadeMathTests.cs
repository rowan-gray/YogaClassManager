using YogaClassManager.Avalonia.Behaviors;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Covers the pure scroll-position math behind ScrollEdgeFade in isolation from any live
///     ScrollViewer - see UI_STYLE_GUIDE.md's "Scroll edge fade" section for what this drives.
/// </summary>
public class ScrollEdgeFadeMathTests
{
    [Fact]
    public void Compute_ContentFitsEntirely_NoFadeEitherEdge()
    {
        var state = ScrollEdgeFadeMath.Compute(extent: 150, viewport: 200, offset: 0, fadeHeight: 24);

        Assert.False(state.CanScrollUp);
        Assert.False(state.CanScrollDown);
    }

    [Fact]
    public void Compute_ScrolledToTop_FadesBottomOnly()
    {
        var state = ScrollEdgeFadeMath.Compute(extent: 1000, viewport: 200, offset: 0, fadeHeight: 24);

        Assert.False(state.CanScrollUp);
        Assert.True(state.CanScrollDown);
    }

    [Fact]
    public void Compute_ScrolledToBottom_FadesTopOnly()
    {
        var state = ScrollEdgeFadeMath.Compute(extent: 1000, viewport: 200, offset: 800, fadeHeight: 24);

        Assert.True(state.CanScrollUp);
        Assert.False(state.CanScrollDown);
    }

    [Fact]
    public void Compute_ScrolledInMiddle_FadesBothEdges()
    {
        var state = ScrollEdgeFadeMath.Compute(extent: 1000, viewport: 200, offset: 400, fadeHeight: 24);

        Assert.True(state.CanScrollUp);
        Assert.True(state.CanScrollDown);
    }

    [Fact]
    public void Compute_TinyViewport_ClampsFadeStopsToFiftyPercentWithoutCrossing()
    {
        var state = ScrollEdgeFadeMath.Compute(extent: 1000, viewport: 30, offset: 400, fadeHeight: 24);

        Assert.Equal(0.5, state.TopStopOffset);
        Assert.Equal(0.5, state.BottomStopOffset);
    }

    [Fact]
    public void Compute_ZeroViewport_ReturnsNoFade()
    {
        var state = ScrollEdgeFadeMath.Compute(extent: 1000, viewport: 0, offset: 0, fadeHeight: 24);

        Assert.False(state.CanScrollUp);
        Assert.False(state.CanScrollDown);
    }
}
