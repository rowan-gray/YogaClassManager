using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using YogaClassManager.Core.Data;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     A scrollable list that pages in more of its IGrowableCollection as the user scrolls near the
///     bottom, mirroring the MAUI app's GrowableScrollView. RemainingItemsThreshold is measured in
///     pixels from the bottom here (a plain ScrollViewer has no per-item count the way MAUI's
///     CollectionView does), which is a deliberate simplification of the original per-item threshold.
/// </summary>
public partial class GrowableItemsControl : UserControl
{
    public static readonly StyledProperty<IGrowableCollection?> ItemsSourceProperty =
        AvaloniaProperty.Register<GrowableItemsControl, IGrowableCollection?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<GrowableItemsControl, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<uint> GrowAmountProperty =
        AvaloniaProperty.Register<GrowableItemsControl, uint>(nameof(GrowAmount), 20);

    public static readonly StyledProperty<double> RemainingItemsThresholdProperty =
        AvaloniaProperty.Register<GrowableItemsControl, double>(nameof(RemainingItemsThreshold), 200);

    public static readonly DirectProperty<GrowableItemsControl, bool> IsGrowingProperty =
        AvaloniaProperty.RegisterDirect<GrowableItemsControl, bool>(nameof(IsGrowing), o => o.isGrowing);

    private bool isGrowing;
    private bool exhausted;

    public GrowableItemsControl()
    {
        InitializeComponent();
    }

    public IGrowableCollection? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set
        {
            SetValue(ItemsSourceProperty, value);
            exhausted = false;
        }
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public uint GrowAmount
    {
        get => GetValue(GrowAmountProperty);
        set => SetValue(GrowAmountProperty, value);
    }

    public double RemainingItemsThreshold
    {
        get => GetValue(RemainingItemsThresholdProperty);
        set => SetValue(RemainingItemsThresholdProperty, value);
    }

    public bool IsGrowing => isGrowing;

    private async void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (isGrowing || exhausted || ItemsSource is null || Scroller is null)
            return;

        var distanceFromBottom = Scroller.Extent.Height - Scroller.Viewport.Height - Scroller.Offset.Y;
        if (distanceFromBottom > RemainingItemsThreshold)
            return;

        SetAndRaise(IsGrowingProperty, ref isGrowing, true);
        try
        {
            var grown = await ItemsSource.GrowCollection(GrowAmount);
            if (grown < GrowAmount)
                exhausted = true;
        }
        finally
        {
            SetAndRaise(IsGrowingProperty, ref isGrowing, false);
        }
    }
}
