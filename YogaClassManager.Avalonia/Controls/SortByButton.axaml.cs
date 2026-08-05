using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using YogaClassManager.Core.Data;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Standalone "Sort by: field ↑/↓" dropdown button - see UI_STYLE_GUIDE.md's "Sort and filter
///     controls" section. Composed by FilterBar for pages that also need a search box/basic filters;
///     used directly (no FilterBar) for smaller lists that just need sort, e.g. a LinkedRecordsPanel.
/// </summary>
public partial class SortByButton : UserControl
{
    public static readonly StyledProperty<IEnumerable?> SortOptionsProperty =
        AvaloniaProperty.Register<SortByButton, IEnumerable?>(nameof(SortOptions));

    public static readonly StyledProperty<object?> SelectedSortProperty =
        AvaloniaProperty.Register<SortByButton, object?>(nameof(SelectedSort), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable?> SortOrderOptionsProperty =
        AvaloniaProperty.Register<SortByButton, IEnumerable?>(nameof(SortOrderOptions));

    public static readonly StyledProperty<object?> SelectedSortOrderProperty =
        AvaloniaProperty.Register<SortByButton, object?>(nameof(SelectedSortOrder),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<SortByButton, string> SortDescriptionProperty =
        AvaloniaProperty.RegisterDirect<SortByButton, string>(nameof(SortDescription), o => o.SortDescription);

    private string sortDescription = DefaultDescription;

    private const string DefaultDescription = "Sort options";

    static SortByButton()
    {
        SelectedSortProperty.Changed.AddClassHandler<SortByButton>((button, _) => button.UpdateSortDescription());
        SelectedSortOrderProperty.Changed.AddClassHandler<SortByButton>((button, _) => button.UpdateSortDescription());
    }

    public SortByButton()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     The button's accessible name and tooltip, e.g. "Sort by Name, ascending". The visible face
    ///     conveys direction with an up/down arrow alone, which is neither announced by a screen reader
    ///     nor distinguishable to someone who can't tell the two glyphs apart at 14px - this states it
    ///     in words instead.
    /// </summary>
    public string SortDescription
    {
        get => sortDescription;
        private set => SetAndRaise(SortDescriptionProperty, ref sortDescription, value);
    }

    private void UpdateSortDescription()
    {
        if (SelectedSort is null)
        {
            SortDescription = DefaultDescription;
            return;
        }

        var direction = SelectedSortOrder switch
        {
            Order.Ascending => ", ascending",
            Order.Descending => ", descending",
            _ => ""
        };

        SortDescription = $"Sort by {SelectedSort}{direction}";
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
