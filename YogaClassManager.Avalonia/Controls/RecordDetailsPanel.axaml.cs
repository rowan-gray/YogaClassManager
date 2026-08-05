using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     A read-only record display, mirroring the MAUI app's
///     DetailsView/ClassDetailsView/TermDetailsView/PassDetailsView (all the same label/value shape,
///     duplicated per entity). Edit lives in the page's own action row alongside the "..." overflow
///     button (see PeopleView/StudentsView), not here.
/// </summary>
public partial class RecordDetailsPanel : UserControl
{
    public static readonly StyledProperty<object?> RecordProperty =
        AvaloniaProperty.Register<RecordDetailsPanel, object?>(nameof(Record));

    public static readonly StyledProperty<IDataTemplate?> RecordTemplateProperty =
        AvaloniaProperty.Register<RecordDetailsPanel, IDataTemplate?>(nameof(RecordTemplate));

    public RecordDetailsPanel()
    {
        InitializeComponent();
    }

    public object? Record
    {
        get => GetValue(RecordProperty);
        set => SetValue(RecordProperty, value);
    }

    public IDataTemplate? RecordTemplate
    {
        get => GetValue(RecordTemplateProperty);
        set => SetValue(RecordTemplateProperty, value);
    }
}
