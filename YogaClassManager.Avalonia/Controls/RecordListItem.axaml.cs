using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     One row in a MasterDetailView's master list: a title, an optional caption beneath it, and an
///     optional status flag. All five list pages hand-rolled this same three-part stack, and all five
///     drew their flag as small coloured text rather than the <see cref="Tag" /> the style guide
///     reserves for exactly that job - so both the layout and the flag treatment now have one home.
///
///     Titles trim rather than clipping, which the fixed 320px master column made necessary and
///     nothing in the app previously did (UI_REVIEW.md D7 - Avalonia's TextTrimming default is None).
/// </summary>
public partial class RecordListItem : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RecordListItem, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<RecordListItem, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> FlagLabelProperty =
        AvaloniaProperty.Register<RecordListItem, string?>(nameof(FlagLabel));

    public static readonly StyledProperty<bool> IsFlaggedProperty =
        AvaloniaProperty.Register<RecordListItem, bool>(nameof(IsFlagged));

    public static readonly StyledProperty<IBrush?> FlagColorProperty =
        AvaloniaProperty.Register<RecordListItem, IBrush?>(nameof(FlagColor));

    public RecordListItem()
    {
        InitializeComponent();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The muted second line - an email, an attendee count. Hidden when empty, so a list whose
    /// rows have no secondary detail stays single-line.</summary>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Short, upper-case, one word ("ARCHIVED", "COMPLETED") - it renders as a
    /// <see cref="Tag" />.</summary>
    public string? FlagLabel
    {
        get => GetValue(FlagLabelProperty);
        set => SetValue(FlagLabelProperty, value);
    }

    public bool IsFlagged
    {
        get => GetValue(IsFlaggedProperty);
        set => SetValue(IsFlaggedProperty, value);
    }

    /// <summary>One of the TagXxxBrush tokens - see UI_STYLE_GUIDE.md's tag-colour notes for why these
    /// are single fixed values rather than Light/Dark pairs.</summary>
    public IBrush? FlagColor
    {
        get => GetValue(FlagColorProperty);
        set => SetValue(FlagColorProperty, value);
    }
}
