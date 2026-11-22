using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace YogaClassManager.Avalonia.Transitions;

/// <summary>
///     A background-colour transition that blends in <b>premultiplied</b> alpha, used instead of
///     Avalonia's <see cref="BrushTransition" /> for every interactive Background in Styles/Controls.axaml.
///     <para>
///         BrushTransition interpolates R, G, B and A independently (straight alpha). Hover/pressed
///         tints are translucent overlays whose colour is white (dark theme) or black (light theme),
///         and "no background" is transparent white - so blending one of them towards an opaque
///         selection colour drags the colour channels through white/black while alpha is still
///         climbing. Halfway from a dark-theme pressed tint (#33FFFFFF) to the selection blue
///         (#004175) is ~60% alpha of a pale grey-blue: over the dark card that renders <i>lighter
///         than either end</i>, a grey flash every time the selection changes.
///     </para>
///     <para>
///         Interpolating premultiplied colour (channel x alpha) is the same as cross-fading the two
///         <i>rendered</i> results on whatever is behind them, so the in-between frames always lie
///         between the start and end colours - no flash, on any background, in either theme. A brush's
///         Opacity is folded into its alpha first, so a translucent brush animates correctly too.
///     </para>
/// </summary>
public class PremultipliedBrushTransition : InterpolatingTransitionBase<IBrush?>
{
    protected override IBrush? Interpolate(double progress, IBrush? from, IBrush? to)
    {
        // Anything that isn't a plain colour (gradients etc.) can't be blended this way - switch over
        // at the halfway point, as BrushTransition does for brush types it can't interpolate.
        if (!TryGetColor(from, out var start) || !TryGetColor(to, out var end))
            return progress < 0.5 ? from : to;

        return new ImmutableSolidColorBrush(Blend(start, end, progress));
    }

    /// <summary>Premultiplied-alpha blend of two colours; <paramref name="progress" /> 0 gives
    /// <paramref name="from" />, 1 gives <paramref name="to" />. Public for the tests.</summary>
    public static Color Blend(Color from, Color to, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);

        double fromA = from.A / 255.0, toA = to.A / 255.0;
        var a = fromA + (toA - fromA) * progress;
        if (a <= 0)
            return Color.FromArgb(0, to.R, to.G, to.B);

        byte Channel(byte fromChannel, byte toChannel)
        {
            var premultiplied = fromChannel * fromA + (toChannel * toA - fromChannel * fromA) * progress;
            return (byte)Math.Clamp(Math.Round(premultiplied / a), 0, 255);
        }

        return Color.FromArgb((byte)Math.Round(a * 255), Channel(from.R, to.R), Channel(from.G, to.G),
            Channel(from.B, to.B));
    }

    /// <summary>A solid brush's colour with its Opacity folded into alpha; "no brush" is fully
    /// transparent.</summary>
    private static bool TryGetColor(IBrush? brush, out Color color)
    {
        switch (brush)
        {
            case null:
                color = Colors.Transparent;
                return true;
            case ISolidColorBrush solid:
                color = Color.FromArgb((byte)Math.Round(solid.Color.A * Math.Clamp(solid.Opacity, 0, 1)),
                    solid.Color.R, solid.Color.G, solid.Color.B);
                return true;
            default:
                color = default;
                return false;
        }
    }
}
