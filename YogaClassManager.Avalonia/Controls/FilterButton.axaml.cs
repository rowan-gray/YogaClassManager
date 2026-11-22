using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Standalone filter dropdown button - see UI_STYLE_GUIDE.md's "Sort and filter controls" section.
///     Composed by FilterBar for pages that also need a search box/basic filters; used directly (no
///     FilterBar) for smaller lists that just need filters, e.g. the Student page's Passes list.
///     <para>
///         A page's <b>main</b> list also puts its sort inside this popup (set the Sort* properties):
///         the "Sort by" panel sits above the filters, and the button becomes "Sort and filter". The
///         detail pane's sub-lists keep a separate SortByButton instead.
///     </para>
/// </summary>
public partial class FilterButton : UserControl
{
    public static readonly StyledProperty<Control?> FiltersProperty =
        AvaloniaProperty.Register<FilterButton, Control?>(nameof(Filters));

    public static readonly StyledProperty<IEnumerable?> SortOptionsProperty =
        AvaloniaProperty.Register<FilterButton, IEnumerable?>(nameof(SortOptions));

    public static readonly StyledProperty<object?> SelectedSortProperty =
        AvaloniaProperty.Register<FilterButton, object?>(nameof(SelectedSort), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable?> SortOrderOptionsProperty =
        AvaloniaProperty.Register<FilterButton, IEnumerable?>(nameof(SortOrderOptions));

    public static readonly StyledProperty<object?> SelectedSortOrderProperty =
        AvaloniaProperty.Register<FilterButton, object?>(nameof(SelectedSortOrder),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<FilterButton, string> LabelProperty =
        AvaloniaProperty.RegisterDirect<FilterButton, string>(nameof(Label), o => o.Label);

    private string label = "Filters";

    static FilterButton()
    {
        SortOptionsProperty.Changed.AddClassHandler<FilterButton>((button, _) =>
            button.Label = button.SortOptions is null ? "Filters" : "Sort and filter");
    }

    public static readonly StyledProperty<int> FilterCountProperty =
        AvaloniaProperty.Register<FilterButton, int>(nameof(FilterCount));

    public static readonly StyledProperty<ICommand?> ClearFiltersCommandProperty =
        AvaloniaProperty.Register<FilterButton, ICommand?>(nameof(ClearFiltersCommand));

    public FilterButton()
    {
        InitializeComponent();
    }

    public Control? Filters
    {
        get => GetValue(FiltersProperty);
        set => SetValue(FiltersProperty, value);
    }

    /// <summary>How many of Filters' fields are currently non-default - the host computes this
    /// (FilterButton has no way to introspect an arbitrary supplied filter control tree itself).</summary>
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

    /// <summary>When set, the popup starts with a "Sort by" section (SortOptionsPanel) above the
    /// filters - the main-list layout, where sort and filter share one button.</summary>
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

    /// <summary>The button's tooltip and accessible name: "Sort and filter" when the popup includes
    /// sort, else "Filters".</summary>
    public string Label
    {
        get => label;
        private set => SetAndRaise(LabelProperty, ref label, value);
    }
}
