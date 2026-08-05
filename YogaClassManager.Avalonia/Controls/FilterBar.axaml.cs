using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Search box + basic filters + a "Sort by: field ↑/↓" dropdown button + a filter button (just the
///     icon when no filters are active, "🔽 N filters" once any are) - both buttons open a small
///     Flyout (not the shared dialog overlay) with their own controls, rather than an inline expanding
///     panel. BasicFilters and AdvancedFilters are plain Control-typed slots (Avalonia property-element
///     syntax) rather than DataTemplate+Content pairs, since each page just supplies a fixed,
///     already-instantiated set of controls (a CheckBox, some date pickers) - there's no per-item
///     templating need here. Flyout open/closed state is Avalonia's own Button-Flyout attachment
///     behavior, not state this control tracks itself.
/// </summary>
public partial class FilterBar : UserControl
{
    public static readonly StyledProperty<string?> SearchQueryProperty =
        AvaloniaProperty.Register<FilterBar, string?>(nameof(SearchQuery), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> SearchWatermarkProperty =
        AvaloniaProperty.Register<FilterBar, string?>(nameof(SearchWatermark));

    public static readonly StyledProperty<IEnumerable?> SortOptionsProperty =
        AvaloniaProperty.Register<FilterBar, IEnumerable?>(nameof(SortOptions));

    public static readonly StyledProperty<object?> SelectedSortProperty =
        AvaloniaProperty.Register<FilterBar, object?>(nameof(SelectedSort), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable?> SortOrderOptionsProperty =
        AvaloniaProperty.Register<FilterBar, IEnumerable?>(nameof(SortOrderOptions));

    public static readonly StyledProperty<object?> SelectedSortOrderProperty =
        AvaloniaProperty.Register<FilterBar, object?>(nameof(SelectedSortOrder),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Control?> BasicFiltersProperty =
        AvaloniaProperty.Register<FilterBar, Control?>(nameof(BasicFilters));

    public static readonly StyledProperty<Control?> AdvancedFiltersProperty =
        AvaloniaProperty.Register<FilterBar, Control?>(nameof(AdvancedFilters));

    public static readonly StyledProperty<int> FilterCountProperty =
        AvaloniaProperty.Register<FilterBar, int>(nameof(FilterCount));

    public static readonly StyledProperty<ICommand?> ClearFiltersCommandProperty =
        AvaloniaProperty.Register<FilterBar, ICommand?>(nameof(ClearFiltersCommand));

    public FilterBar()
    {
        InitializeComponent();
    }

    public string? SearchQuery
    {
        get => GetValue(SearchQueryProperty);
        set => SetValue(SearchQueryProperty, value);
    }

    public string? SearchWatermark
    {
        get => GetValue(SearchWatermarkProperty);
        set => SetValue(SearchWatermarkProperty, value);
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

    public Control? BasicFilters
    {
        get => GetValue(BasicFiltersProperty);
        set => SetValue(BasicFiltersProperty, value);
    }

    public Control? AdvancedFilters
    {
        get => GetValue(AdvancedFiltersProperty);
        set => SetValue(AdvancedFiltersProperty, value);
    }

    /// <summary>How many of AdvancedFilters' fields are currently non-default - the page ViewModel
    /// computes this (FilterBar has no way to introspect arbitrary supplied filter controls itself).</summary>
    public int FilterCount
    {
        get => GetValue(FilterCountProperty);
        set => SetValue(FilterCountProperty, value);
    }

    /// <summary>Resets whatever AdvancedFilters represents back to defaults - page-supplied, same
    /// reasoning as FilterCount.</summary>
    public ICommand? ClearFiltersCommand
    {
        get => GetValue(ClearFiltersCommandProperty);
        set => SetValue(ClearFiltersCommandProperty, value);
    }
}
