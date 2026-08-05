using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     A small rounded, colored-background label for status indicators (e.g. Expired/Depleted on a
///     Pass) - replaces the previous bold-colored-text convention for exactly this kind of "one-word
///     status flag" case. <see cref="Color" /> is expected to be a fill solid enough for white text at
///     WCAG AA contrast in both themes (see the Tag*Brush tokens in Styles/Tokens.axaml, which are
///     purpose-built for this and deliberately don't flip hue per theme the way plain-text semantic
///     brushes do - the badge's own fill is the "background" here, not the page).
/// </summary>
public partial class Tag : UserControl
{
    public static readonly StyledProperty<IBrush?> ColorProperty =
        AvaloniaProperty.Register<Tag, IBrush?>(nameof(Color));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Tag, string?>(nameof(Label));

    public Tag()
    {
        InitializeComponent();
    }

    public IBrush? Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}
