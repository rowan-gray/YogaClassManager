using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Standalone filter dropdown button - see UI_STYLE_GUIDE.md's "Sort and filter controls" section.
///     Composed by FilterBar for pages that also need a search box/basic filters; used directly (no
///     FilterBar) for smaller lists that just need filters, e.g. the Student page's Passes list.
/// </summary>
public partial class FilterButton : UserControl
{
    public static readonly StyledProperty<Control?> FiltersProperty =
        AvaloniaProperty.Register<FilterButton, Control?>(nameof(Filters));

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
}
