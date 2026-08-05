using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Generic list+detail split, mirroring the master-detail pattern behind the MAUI app's
///     CollectionPageModel&lt;T&gt; UI (ClassesPage/TermsPage/StudentsPage/PeoplePage all repeated this
///     by hand). One control, reused with different item/detail templates per page.
/// </summary>
public partial class MasterDetailView : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<MasterDetailView, IEnumerable?>(nameof(Items));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<MasterDetailView, object?>(nameof(SelectedItem),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IDataTemplate?> ListItemTemplateProperty =
        AvaloniaProperty.Register<MasterDetailView, IDataTemplate?>(nameof(ListItemTemplate));

    public static readonly StyledProperty<IDataTemplate?> DetailTemplateProperty =
        AvaloniaProperty.Register<MasterDetailView, IDataTemplate?>(nameof(DetailTemplate));

    public static readonly StyledProperty<string> NoSelectionTextProperty =
        AvaloniaProperty.Register<MasterDetailView, string>(nameof(NoSelectionText),
            "Select an item to view details.");

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<MasterDetailView, string?>(nameof(EmptyText), "Nothing to show.");

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<MasterDetailView, string?>(nameof(ErrorText));

    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<MasterDetailView, bool>(nameof(IsBusy));

    public static readonly DirectProperty<MasterDetailView, bool> IsEmptyProperty =
        AvaloniaProperty.RegisterDirect<MasterDetailView, bool>(nameof(IsEmpty), o => o.IsEmpty);

    private readonly CollectionEmptinessWatcher emptiness;
    private bool isEmpty = true;

    static MasterDetailView()
    {
        ItemsProperty.Changed.AddClassHandler<MasterDetailView>(
            (view, args) => view.emptiness.Watch(args.NewValue as IEnumerable));
    }

    public MasterDetailView()
    {
        emptiness = new CollectionEmptinessWatcher(empty => IsEmpty = empty);
        InitializeComponent();
    }

    /// <summary>Shown in the master pane when the list is empty and not loading - the case that
    /// otherwise renders as a blank card indistinguishable from "still loading". Pages should override
    /// the default with something search-aware ("No students match this search.").</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>Bound to the page's LastError. Non-null pins a message above the list, so a failed load
    /// isn't indistinguishable from an empty one once the transient error toast has gone.</summary>
    public string? ErrorText
    {
        get => GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    public bool IsBusy
    {
        get => GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public bool IsEmpty
    {
        get => isEmpty;
        private set => SetAndRaise(IsEmptyProperty, ref isEmpty, value);
    }

    public IEnumerable? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public IDataTemplate? ListItemTemplate
    {
        get => GetValue(ListItemTemplateProperty);
        set => SetValue(ListItemTemplateProperty, value);
    }

    public IDataTemplate? DetailTemplate
    {
        get => GetValue(DetailTemplateProperty);
        set => SetValue(DetailTemplateProperty, value);
    }

    public string NoSelectionText
    {
        get => GetValue(NoSelectionTextProperty);
        set => SetValue(NoSelectionTextProperty, value);
    }
}
