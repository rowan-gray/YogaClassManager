using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Guards the interactive-state invariant documented in Styles/Controls.axaml and
///     UI_STYLE_GUIDE.md: <b>a BrushTransition may only be attached where the app owns an Opacity=1
///     brush for every state at both ends of the change.</b>
///
///     The bug this prevents is subtle enough that it was fixed per-class three times without anyone
///     spotting the general rule. Avalonia's SolidColorBrushAnimator interpolates a brush's Color and
///     its Opacity as two independent factors that multiply at render time. FluentTheme's
///     ButtonBackgroundPointerOver is `Color=Black, Opacity=0.1` against a resting
///     `Color=#33000000, Opacity=1`, so a transition between them runs alpha 0x33 -> 0xFF while
///     opacity runs 1.0 -> 0.1 and the midpoint renders *darker than either endpoint* before
///     settling. That overshoot is the grey flash. Every brush in the app's palette therefore keeps
///     Opacity=1 and carries translucency in the colour's alpha channel, where interpolation is
///     monotonic.
///
///     <see cref="EveryAnimatedStateBrush_IsOpaqueSoItCannotOvershoot" /> is the test that generalises:
///     it fails for any *new* state added without an app-owned opaque brush, rather than waiting for
///     someone to notice a flash by eye.
/// </summary>
public class InteractiveStateTests
{
    /// <summary>Renders a control inside a real window so styles apply and templates are built.</summary>
    private static T Show<T>(T control) where T : Control
    {
        var window = new Window { Content = control, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return control;
    }

    /// <summary>The template part FluentTheme actually repaints for hover/pressed - not the control's
    /// own Background, which is why every state rule in Controls.axaml targets it.</summary>
    private static ContentPresenter Presenter(Control control) =>
        control.GetVisualDescendants().OfType<ContentPresenter>().First();

    private static SolidColorBrush Resource(string key, ThemeVariant variant)
    {
        Assert.True(Application.Current!.TryGetResource(key, variant, out var value),
            $"{key} is not defined - the palette lives in Styles/Tokens.axaml");

        return Assert.IsType<SolidColorBrush>(value);
    }

    /// <summary>Forces a pseudo-class on rather than driving real pointer input. Headless Avalonia
    /// reports no pointer device, so a synthesised MouseMove never sets :pointerover; setting the
    /// pseudo-class directly exercises the same style selectors, which is what's under test here.</summary>
    private static void SetState(Control control, string pseudoClass, bool active)
    {
        ((IPseudoClasses)control.Classes).Set(pseudoClass, active);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    ///     The brush the *style system* assigns for the current state, read via GetBaseValue so the
    ///     in-flight BrushTransition is excluded.
    ///
    ///     Reading <c>.Background</c> directly returns whatever frame the 120ms animation happens to be
    ///     on - the first draft of this test asserted against that and saw #32000000, i.e. the resting
    ///     #33000000 having barely started to move. Which state a selector resolves to is the thing
    ///     under test and is deterministic; where the animation had got to is neither.
    /// </summary>
    private static Color BackgroundOf(Control control)
    {
        var presenter = Presenter(control);
        var value = presenter.GetBaseValue(ContentPresenter.BackgroundProperty);

        Assert.True(value.HasValue, "no style assigned a Background for this state");

        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(value.Value);

        // Opacity is the whole point: a brush carrying opacity < 1 is what makes the animator's two
        // factors diverge and overshoot mid-transition.
        Assert.Equal(1d, brush.Opacity);

        return brush.Color;
    }

    [AvaloniaTheory]
    [InlineData(":pointerover", "ButtonFillHoverBrush")]
    [InlineData(":pressed", "ButtonFillPressedBrush")]
    [InlineData(":disabled", "ButtonFillDisabledBrush")]
    public void APlainButton_UsesTheAppPalette(string pseudoClass, string expectedKey)
    {
        // The set that was flashing: SortByButton, FilterButton and the Dashboard's "Mark roll" are
        // all plain default-background Buttons.
        var button = Show(new Button { Content = "Mark roll" });

        SetState(button, pseudoClass, true);

        Assert.Equal(Resource(expectedKey, ThemeVariant.Light).Color, BackgroundOf(button));
    }

    [AvaloniaTheory]
    [InlineData(":pointerover", "AccentActionHoverBrush")]
    [InlineData(":pressed", "AccentActionPressedBrush")]
    [InlineData(":disabled", "AccentActionDisabledBrush")]
    public void AnAccentButton_KeepsItsAccentPalette(string pseudoClass, string expectedKey)
    {
        // Guards the specific regression a naive base-Button rule would cause: every Save/Add button
        // turning grey on hover.
        var button = Show(new Button { Content = "Save", Classes = { "accent" } });

        SetState(button, pseudoClass, true);

        Assert.Equal(Resource(expectedKey, ThemeVariant.Light).Color, BackgroundOf(button));
    }

    [AvaloniaTheory]
    [InlineData("Overflow", ":pointerover", "InteractiveHoverBrush")]
    [InlineData("Overflow", ":pressed", "InteractivePressedBrush")]
    [InlineData("NavItem", ":pointerover", "InteractiveHoverBrush")]
    [InlineData("NavItem", ":pressed", "InteractivePressedBrush")]
    public void AButtonOnACustomSurface_UsesTheOverlayPalette(string className, string pseudoClass,
        string expectedKey)
    {
        // Overflow and NavItem rest on a Card/pane rather than the default button fill, so they take
        // the overlay half of the palette instead of the filled half.
        var button = Show(new Button { Classes = { className } });

        SetState(button, pseudoClass, true);

        Assert.Equal(Resource(expectedKey, ThemeVariant.Light).Color, BackgroundOf(button));
    }

    [AvaloniaFact]
    public void ASelectedNavItem_StaysAccentColouredWhilePressed()
    {
        // Previously fell through to the plain grey :pressed rule, so the selection appeared to blink
        // out mid-click.
        var button = Show(new Button { Classes = { "NavItem", "Selected" } });

        SetState(button, ":pressed", true);

        Assert.Equal(Resource("InteractiveSelectedPressedBrush", ThemeVariant.Light).Color,
            BackgroundOf(button));
    }

    [AvaloniaTheory]
    [InlineData(":pointerover", "InteractiveHoverBrush")]
    [InlineData(":pressed", "InteractivePressedBrush")]
    [InlineData(":selected", "InteractiveSelectedBrush")]
    public void AListRow_UsesTheOverlayPalette(string pseudoClass, string expectedKey)
    {
        var item = new ListBoxItem { Content = "Ada Lovelace" };
        Show(new ListBox { ItemsSource = new[] { item } });

        SetState(item, pseudoClass, true);

        Assert.Equal(Resource(expectedKey, ThemeVariant.Light).Color, BackgroundOf(item));
    }

    [AvaloniaFact]
    public void ASelectedListRow_StaysSelectionColouredWhilePressed()
    {
        var item = new ListBoxItem { Content = "Ada Lovelace" };
        Show(new ListBox { ItemsSource = new[] { item } });

        SetState(item, ":selected", true);
        SetState(item, ":pressed", true);

        Assert.Equal(Resource("InteractiveSelectedPressedBrush", ThemeVariant.Light).Color,
            BackgroundOf(item));
    }

    /// <summary>Names rather than ThemeVariant instances: xunit has to serialise theory data, and a
    /// ThemeVariant isn't serialisable - passing one yields "expected 2 parameter values, but 0 were
    /// provided" at run time rather than a compile error.</summary>
    public static TheoryData<string, string> PaletteKeys()
    {
        string[] keys =
        [
            "InteractiveHoverBrush", "InteractivePressedBrush",
            "InteractiveSelectedBrush", "InteractiveSelectedHoverBrush", "InteractiveSelectedPressedBrush",
            "ButtonFillHoverBrush", "ButtonFillPressedBrush", "ButtonFillDisabledBrush",
            "AccentActionBrush", "AccentActionHoverBrush", "AccentActionPressedBrush",
            "AccentActionDisabledBrush"
        ];

        var data = new TheoryData<string, string>();
        foreach (var key in keys)
        foreach (var variant in new[] { "Light", "Dark" })
            data.Add(key, variant);

        return data;
    }

    [AvaloniaTheory]
    [MemberData(nameof(PaletteKeys))]
    public void EveryAnimatedStateBrush_IsOpaqueSoItCannotOvershoot(string key, string variantName)
    {
        // The general guard. A brush with Opacity < 1 animates its opacity and its colour as separate
        // multiplying factors, which is what produced the flash; alpha in the colour channel does not.
        // Every entry must also exist in BOTH variants, since a key missing from one falls back to the
        // other's value or to null - and a null endpoint degrades the transition to a hold-then-snap.
        var variant = variantName == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        var brush = Resource(key, variant);

        Assert.Equal(1d, brush.Opacity);
    }
}
