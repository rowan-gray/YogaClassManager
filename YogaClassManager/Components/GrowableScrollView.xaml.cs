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
            typeof(GrowableScrollView), (uint)1);

    private bool isGrowing;

    public GrowableScrollView()
    {
        InitializeComponent();
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

    private async void CollectionView_OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
        if (isGrowing) return;

        isGrowing = true;
        await ItemsSource.GrowCollection(GrowAmount);
        isGrowing = false;
    }
}