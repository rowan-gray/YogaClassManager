using YogaClassManager.NewModels;

namespace YogaClassManager.Components;

public partial class GrowableScrollView : ContentView
{
    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate),
            typeof(GrowableScrollView));

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IGrowableCollection),
            typeof(GrowableScrollView));

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object),
            typeof(GrowableScrollView));

    public static readonly BindableProperty GrowAmountProperty =
        BindableProperty.Create(nameof(GrowAmount), typeof(uint),
            typeof(GrowableScrollView), (uint)5);

    public static readonly BindableProperty RemainingItemsThresholdProperty =
        BindableProperty.Create(nameof(RemainingItemsThreshold), typeof(uint),
            typeof(GrowableScrollView), (uint)5);
    
    private GrowableScrollViewStatus status;

    public GrowableScrollView()
    {
        InitializeComponent();
    }

    public GrowableScrollViewStatus Status
    {
        get => status;
        set
        {
            status = value;
            OnPropertyChanged();
        }
    }

    public DataTemplate ItemTemplate
    {
        get => (DataTemplate)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public IGrowableCollection ItemsSource
    {
        get => (IGrowableCollection)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public uint GrowAmount
    {
        get => (uint)GetValue(GrowAmountProperty);
        set => SetValue(GrowAmountProperty, value);
    }

    public uint RemainingItemsThreshold
    {
        get => (uint)GetValue(RemainingItemsThresholdProperty);
        set => SetValue(RemainingItemsThresholdProperty, value);
    }

    private async void CollectionView_OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
        if (Status is GrowableScrollViewStatus.Growing or GrowableScrollViewStatus.Exhausted) return;

        Status = GrowableScrollViewStatus.Growing;
        
        var count = await ItemsSource.GrowCollection(GrowAmount);
        await Task.Delay(250);
        
        Status = count >= GrowAmount ? GrowableScrollViewStatus.None : GrowableScrollViewStatus.Exhausted;
    }
}

public enum GrowableScrollViewStatus
{
    None,
    Growing,
    Full,
    Exhausted
}