using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit.Abstractions;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     TEMPORARY probe - delete once the palette is seeded. Prints what FluentTheme's interactive
///     state resources actually resolve to at runtime, so the app's own palette can be seeded with
///     real values instead of guesses, and so the "incompatible BrushTransition pair" hypothesis can
///     be confirmed by looking at the runtime types.
/// </summary>
public class ThemeBrushProbe(ITestOutputHelper output)
{
    private static readonly string[] Keys =
    [
        "ButtonBackground",
        "ButtonBackgroundPointerOver",
        "ButtonBackgroundPressed",
        "ButtonBackgroundDisabled",
        "AccentButtonBackground",
        "AccentButtonBackgroundPointerOver",
        "AccentButtonBackgroundPressed",
        "AccentButtonBackgroundDisabled",
        "SystemListLowColor",
        "SystemListMediumColor",
        "SystemAccentColor",
        "SystemAccentColorLight1",
        "SystemAccentColorLight2",
        "SystemAccentColorLight3",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAltHighColor",
        "SystemBaseLowColor",
        "SystemAltMediumLowColor",
        "ControlFillColorDefaultBrush",
        "ControlFillColorSecondaryBrush",
        "ControlFillColorTertiaryBrush",
        "ControlFillColorDisabledBrush"
    ];

    [AvaloniaFact]
    public void Dump()
    {
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            output.WriteLine($"===== {variant} =====");

            foreach (var key in Keys)
            {
                if (Application.Current!.TryGetResource(key, variant, out var value) && value is not null)
                    output.WriteLine($"{key,-38} {Describe(value)}");
                else
                    output.WriteLine($"{key,-38} <<UNRESOLVED>>");
            }
        }
    }

    private static string Describe(object value) => value switch
    {
        ISolidColorBrush solid =>
            $"{value.GetType().Name} ISolidColorBrush color={solid.Color} opacity={solid.Opacity}",
        Color color => $"Color {color}",
        IBrush brush => $"{value.GetType().Name} IBrush (NOT solid) opacity={brush.Opacity}",
        _ => $"{value.GetType().Name} {value}"
    };
}
