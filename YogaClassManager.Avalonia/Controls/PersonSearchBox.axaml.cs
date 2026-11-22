using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Material.Icons;
using YogaClassManager.Avalonia.ViewModels.Shared;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     The shared "find a person by name" search box and results list. Bind its DataContext to a
///     <see cref="PersonSearchViewModel{TPerson}" />. Each row can carry an action button
///     (<see cref="ShowRowAction" />, <see cref="ActionLabel" />, <see cref="ActionIcon" /> - "Add to
///     roll" / Plus in the Mark Roll window); a picker dialog turns it off, since there the dialog's own
///     Select button (and Enter, or double-clicking a row) does the choosing.
/// </summary>
public partial class PersonSearchBox : UserControl
{
    public static readonly StyledProperty<bool> ShowRowActionProperty =
        AvaloniaProperty.Register<PersonSearchBox, bool>(nameof(ShowRowAction), true);

    public static readonly StyledProperty<string> ActionLabelProperty =
        AvaloniaProperty.Register<PersonSearchBox, string>(nameof(ActionLabel), "Select");

    public static readonly StyledProperty<MaterialIconKind> ActionIconProperty =
        AvaloniaProperty.Register<PersonSearchBox, MaterialIconKind>(nameof(ActionIcon), MaterialIconKind.Check);

    public PersonSearchBox()
    {
        InitializeComponent();

        // Double-clicking a row chooses it - the mouse equivalent of arrowing to it and pressing Enter.
        ResultsList.DoubleTapped += OnResultDoubleTapped;
    }

    /// <summary>Whether each result row shows its action button (default true).</summary>
    public bool ShowRowAction
    {
        get => GetValue(ShowRowActionProperty);
        set => SetValue(ShowRowActionProperty, value);
    }

    /// <summary>Each row button's tooltip, and the start of its accessible name ("Add to roll: Ada
    /// Lovelace").</summary>
    public string ActionLabel
    {
        get => GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    public MaterialIconKind ActionIcon
    {
        get => GetValue(ActionIconProperty);
        set => SetValue(ActionIconProperty, value);
    }

    private void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Only a double-click on an actual row (the first click selects it), not on empty list space.
        if (ResultsList.SelectedItem is null || DataContext is not IPersonSearch search)
            return;

        if (search.ChooseSelectedCommand.CanExecute(null))
            search.ChooseSelectedCommand.Execute(null);
    }
}
