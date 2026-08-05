using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     One labelled form input: a caption above whatever control is supplied as Content.
///
///     Two reasons this is a control and not the loose "TextBlock then TextBox in a StackPanel" pair it
///     replaces. It was the single most-duplicated structure in the app (~25 copies, each free to drift
///     in spacing), and - the part markup can't express - it wires
///     <see cref="AutomationProperties.LabeledByProperty" /> from the input to its own label, so a
///     screen reader announces "First name, edit" instead of just "edit". A loose TextBlock beside an
///     input is not a label as far as accessibility is concerned; only that association makes it one.
///
///     <code>
///     &lt;controls:FormField Label="First name"&gt;
///         &lt;TextBox Text="{Binding FirstName}" /&gt;
///     &lt;/controls:FormField&gt;
///     </code>
///
///     The template lives in Styles/Controls.axaml alongside the other shared control styling.
/// </summary>
public class FormField : ContentControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FormField, string?>(nameof(Label));

    private TextBlock? labelBlock;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        labelBlock = e.NameScope.Find<TextBlock>("PART_Label");
        AssociateLabel();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Content is usually set once in markup, but re-associating on change keeps this correct for a
        // field whose input is swapped at runtime rather than silently pointing at the old one.
        if (change.Property == ContentProperty)
            AssociateLabel();
    }

    private void AssociateLabel()
    {
        if (labelBlock is not null && Content is Control field)
            AutomationProperties.SetLabeledBy(field, labelBlock);
    }
}
