using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     A header + list of linked records with optional Add/Edit/Remove commands, mirroring the MAUI
///     app's EmergencyContactsView/PassesView/HealthConcernsContentView (all the same ItemsSource +
///     Selected* + nullable Add/Edit/RemoveCommand shape). Buttons auto-hide when their command is
///     null, so the same panel works read-only (e.g. attendance/usage history) or editable.
///     SortOptions/Filters (+ their companion properties) give the list native sort/filter support -
///     see UI_STYLE_GUIDE.md's "Sort and filter controls" section - rendering a SortByButton/
///     FilterButton in the header row automatically (each hidden when its respective SortOptions/
///     Filters is null, same as FilterBar's own composition of the two). HeaderContent remains a
///     general-purpose slot for anything else that doesn't fit that shape.
/// </summary>
public partial class LinkedRecordsPanel : UserControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, string?>(nameof(Header));

    public static readonly StyledProperty<Control?> HeaderContentProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, Control?>(nameof(HeaderContent));

    public static readonly StyledProperty<IEnumerable?> SortOptionsProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, IEnumerable?>(nameof(SortOptions));

    public static readonly StyledProperty<object?> SelectedSortProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, object?>(nameof(SelectedSort),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable?> SortOrderOptionsProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, IEnumerable?>(nameof(SortOrderOptions));

    public static readonly StyledProperty<object?> SelectedSortOrderProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, object?>(nameof(SelectedSortOrder),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Control?> FiltersProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, Control?>(nameof(Filters));

    public static readonly StyledProperty<int> FilterCountProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, int>(nameof(FilterCount));

    public static readonly StyledProperty<ICommand?> ClearFiltersCommandProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, ICommand?>(nameof(ClearFiltersCommand));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, bool>(nameof(IsLoading));

    public static readonly StyledProperty<int> SkeletonRowsProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, int>(nameof(SkeletonRows), 3);

    public static readonly StyledProperty<bool> SkeletonShowsSubtitleProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, bool>(nameof(SkeletonShowsSubtitle), true);

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, ICommand?>(nameof(AddCommand));

    public static readonly StyledProperty<string> EditLabelProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, string>(nameof(EditLabel), "Edit");

    public static readonly StyledProperty<string> ViewLabelProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, string>(nameof(ViewLabel), "View");

    public static readonly StyledProperty<ICommand?> ViewCommandProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, ICommand?>(nameof(ViewCommand));

    public static readonly StyledProperty<ICommand?> EditCommandProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, ICommand?>(nameof(EditCommand));

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, ICommand?>(nameof(RemoveCommand));

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, string?>(nameof(EmptyText));

    public static readonly DirectProperty<LinkedRecordsPanel, bool> IsEmptyProperty =
        AvaloniaProperty.RegisterDirect<LinkedRecordsPanel, bool>(nameof(IsEmpty), o => o.IsEmpty);

    public static readonly StyledProperty<bool> ShowCountProperty =
        AvaloniaProperty.Register<LinkedRecordsPanel, bool>(nameof(ShowCount));

    public static readonly DirectProperty<LinkedRecordsPanel, int> ItemCountProperty =
        AvaloniaProperty.RegisterDirect<LinkedRecordsPanel, int>(nameof(ItemCount), o => o.ItemCount);

    private readonly CollectionEmptinessWatcher emptiness;
    private bool isEmpty = true;
    private int itemCount;

    static LinkedRecordsPanel()
    {
        ItemsSourceProperty.Changed.AddClassHandler<LinkedRecordsPanel>(
            (panel, args) => panel.emptiness.Watch(args.NewValue as IEnumerable));
    }

    public LinkedRecordsPanel()
    {
        // The watcher re-checks on every collection change, so the count rides along with it.
        emptiness = new CollectionEmptinessWatcher(empty =>
        {
            IsEmpty = empty;
            ItemCount = ItemsSource?.Cast<object>().Count() ?? 0;
        });
        InitializeComponent();
    }

    /// <summary>What to show instead of a blank card when the list has no items ("No health concerns recorded."). A
    /// panel with no EmptyText renders nothing extra, so this stays opt-in.</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public bool IsEmpty
    {
        get => isEmpty;
        private set => SetAndRaise(IsEmptyProperty, ref isEmpty, value);
    }

    /// <summary>Shows how many items the list holds beside the header ("Attendees 12"), kept current as
    /// items are added and removed. Opt-in: most panels' counts aren't worth the space.</summary>
    public bool ShowCount
    {
        get => GetValue(ShowCountProperty);
        set => SetValue(ShowCountProperty, value);
    }

    public int ItemCount
    {
        get => itemCount;
        private set => SetAndRaise(ItemCountProperty, ref itemCount, value);
    }

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Optional extra content in the header row, between the sort/filter buttons and the Add
    /// button - for a caller that needs more than sort/filter and Add there.</summary>
    public Control? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public IEnumerable? SortOptions
    {
        get => GetValue(SortOptionsProperty);
        set => SetValue(SortOptionsProperty, value);
    }

    public object? SelectedSort
    {
        get => GetValue(SelectedSortProperty);
        set => SetValue(SelectedSortProperty, value);
    }

    public IEnumerable? SortOrderOptions
    {
        get => GetValue(SortOrderOptionsProperty);
        set => SetValue(SortOrderOptionsProperty, value);
    }

    public object? SelectedSortOrder
    {
        get => GetValue(SelectedSortOrderProperty);
        set => SetValue(SelectedSortOrderProperty, value);
    }

    public Control? Filters
    {
        get => GetValue(FiltersProperty);
        set => SetValue(FiltersProperty, value);
    }

    /// <summary>How many of Filters' fields are currently non-default - the host computes this
    /// (LinkedRecordsPanel has no way to introspect an arbitrary supplied filter control tree itself).</summary>
    public int FilterCount
    {
        get => GetValue(FilterCountProperty);
        set => SetValue(FilterCountProperty, value);
    }

    /// <summary>Resets whatever Filters represents back to defaults - host-supplied, same reasoning as
    /// FilterCount.</summary>
    public ICommand? ClearFiltersCommand
    {
        get => GetValue(ClearFiltersCommandProperty);
        set => SetValue(ClearFiltersCommandProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }


    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public ICommand? ViewCommand
    {
        get => GetValue(ViewCommandProperty);
        set => SetValue(ViewCommandProperty, value);
    }

    public ICommand? EditCommand
    {
        get => GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    /// <summary>The owning page is still loading this list (bind its IsLoadingDetails): while the list
    /// is also empty, skeleton rows stand in for it and the empty text stays hidden, so "No passes
    /// match these filters." doesn't flash before the passes arrive.</summary>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>How many skeleton rows to show while loading (default 3).</summary>
    public int SkeletonRows
    {
        get => GetValue(SkeletonRowsProperty);
        set => SetValue(SkeletonRowsProperty, value);
    }

    /// <summary>Whether the skeleton rows have a second line - match the real rows (false for
    /// single-line lists like Health concerns).</summary>
    public bool SkeletonShowsSubtitle
    {
        get => GetValue(SkeletonShowsSubtitleProperty);
        set => SetValue(SkeletonShowsSubtitleProperty, value);
    }

    /// <summary>The "..." menu's View item text - "View" by default; say what opens when it isn't the
    /// row's own record (e.g. "View identity" for an emergency contact).</summary>
    public string ViewLabel
    {
        get => GetValue(ViewLabelProperty);
        set => SetValue(ViewLabelProperty, value);
    }

    /// <summary>The "..." menu's Edit item text - "Edit" by default; name the action when it isn't a
    /// general edit (e.g. "Change pass" for a roll's attendee).</summary>
    public string EditLabel
    {
        get => GetValue(EditLabelProperty);
        set => SetValue(EditLabelProperty, value);
    }
}
