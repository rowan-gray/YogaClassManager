using Avalonia;
using Avalonia.Controls;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     The right-aligned Cancel/primary button pair that closes every dialog - previously copied into
///     all 14 of them, which is how two of those copies ended up with a 12px top margin against the
///     others' 16 (UI_REVIEW.md D3).
///
///     It takes only labels. Both commands are resolved from the inherited DataContext, which is always
///     an <see cref="ViewModels.IDialogViewModel" />: Cancel from CancelCommand, the accent button from
///     DefaultCommand - the same command Controls/DialogHost runs on Enter, so the two can't disagree.
/// </summary>
public partial class DialogButtonRow : UserControl
{
    public static readonly StyledProperty<string> CancelLabelProperty =
        AvaloniaProperty.Register<DialogButtonRow, string>(nameof(CancelLabel), "Cancel");

    public static readonly StyledProperty<string> ConfirmLabelProperty =
        AvaloniaProperty.Register<DialogButtonRow, string>(nameof(ConfirmLabel), "Save");

    public DialogButtonRow()
    {
        InitializeComponent();
    }

    public string CancelLabel
    {
        get => GetValue(CancelLabelProperty);
        set => SetValue(CancelLabelProperty, value);
    }

    /// <summary>The primary action's label - a verb matching what it does ("Save", "Link", "Merge"),
    /// not "OK".</summary>
    public string ConfirmLabel
    {
        get => GetValue(ConfirmLabelProperty);
        set => SetValue(ConfirmLabelProperty, value);
    }
}
