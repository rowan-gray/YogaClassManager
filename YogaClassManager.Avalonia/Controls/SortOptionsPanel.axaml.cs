using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     The sort field dropdown and Ascending/Descending choice, hosted in SortByButton's flyout and in
///     FilterButton's flyout (when a page's main list puts sort inside its sort-and-filter popup).
/// </summary>
public partial class SortOptionsPanel : UserControl
{
    public static readonly StyledProperty<IEnumerable?> SortOptionsProperty =
        AvaloniaProperty.Register<SortOptionsPanel, IEnumerable?>(nameof(SortOptions));

    public static readonly StyledProperty<object?> SelectedSortProperty =
        AvaloniaProperty.Register<SortOptionsPanel, object?>(nameof(SelectedSort),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable?> SortOrderOptionsProperty =
        AvaloniaProperty.Register<SortOptionsPanel, IEnumerable?>(nameof(SortOrderOptions));

    public static readonly StyledProperty<object?> SelectedSortOrderProperty =
        AvaloniaProperty.Register<SortOptionsPanel, object?>(nameof(SelectedSortOrder),
            defaultBindingMode: BindingMode.TwoWay);

    public SortOptionsPanel()
    {
        InitializeComponent();
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
}
