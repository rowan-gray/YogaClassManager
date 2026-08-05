using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     A detail pane's heading row: the record title plus that pane's one primary action and its
///     overflow menu, on a single line. Was ~20 lines copied into all five detail panes.
///
///     <see cref="Actions" /> is a plain control slot rather than a MenuFlyout property, matching
///     LinkedRecordsPanel's Filters/HeaderContent: each page's overflow menu items differ, and a
///     caller-supplied Control is the shape already proven to keep its own <c>ElementName=Root</c>
///     bindings working when hosted inside one of these controls.
/// </summary>
public partial class DetailHeader : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DetailHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> PrimaryActionLabelProperty =
        AvaloniaProperty.Register<DetailHeader, string?>(nameof(PrimaryActionLabel));

    public static readonly StyledProperty<ICommand?> PrimaryActionCommandProperty =
        AvaloniaProperty.Register<DetailHeader, ICommand?>(nameof(PrimaryActionCommand));

    public static readonly StyledProperty<Control?> ActionsProperty =
        AvaloniaProperty.Register<DetailHeader, Control?>(nameof(Actions));

    public DetailHeader()
    {
        InitializeComponent();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? PrimaryActionLabel
    {
        get => GetValue(PrimaryActionLabelProperty);
        set => SetValue(PrimaryActionLabelProperty, value);
    }

    /// <summary>The pane's single always-visible action ("Edit", "Mark new roll"). The button hides
    /// itself when this is null, for a read-only pane.</summary>
    public ICommand? PrimaryActionCommand
    {
        get => GetValue(PrimaryActionCommandProperty);
        set => SetValue(PrimaryActionCommandProperty, value);
    }

    /// <summary>Everything else - in practice each page's own <c>Button Classes="Overflow"</c> and its
    /// MenuFlyout, since the items differ per page.</summary>
    public Control? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
