using YogaClassManager.Models.Passes;

namespace YogaClassManager.Views;

public partial class PassesView : ContentView
{
    public static readonly BindableProperty PassesProperty =
        BindableProperty.Create(nameof(Passes), typeof(IEnumerable<Pass>), typeof(PassesView));

    public static readonly BindableProperty SelectedPassProperty =
        BindableProperty.Create(nameof(SelectedPass), typeof(Pass), typeof(PassesView));

    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(Command), typeof(PassesView));

    public static readonly BindableProperty EditCommandParameterProperty =
        BindableProperty.Create(nameof(EditCommandParameter), typeof(object), typeof(PassesView));

    public static readonly BindableProperty AddCommandProperty =
        BindableProperty.Create(nameof(AddCommand), typeof(Command), typeof(PassesView));

    public static readonly BindableProperty AddCommandParameterProperty =
        BindableProperty.Create(nameof(AddCommandParameter), typeof(object), typeof(PassesView));

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(Command), typeof(PassesView));

    public static readonly BindableProperty RemoveCommandParameterProperty =
        BindableProperty.Create(nameof(RemoveCommandParameter), typeof(object), typeof(PassesView));

    public static readonly BindableProperty AdvancedViewCommandProperty =
        BindableProperty.Create(nameof(AdvancedViewCommand), typeof(Command), typeof(PassesView));

    public PassesView()
    {
        InitializeComponent();
    }

    public IEnumerable<Pass> Passes
    {
        get => (IEnumerable<Pass>)GetValue(PassesProperty);
        set => SetValue(PassesProperty, value);
    }

    public Pass SelectedPass
    {
        get => (Pass)GetValue(SelectedPassProperty);
        set => SetValue(SelectedPassProperty, value);
    }

    public Command EditCommand
    {
        get => (Command)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public object EditCommandParameter
    {
        get => GetValue(EditCommandParameterProperty);
        set => SetValue(EditCommandParameterProperty, value);
    }

    public Command AddCommand
    {
        get => (Command)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public object AddCommandParameter
    {
        get => GetValue(AddCommandParameterProperty);
        set => SetValue(AddCommandParameterProperty, value);
    }

    public Command RemoveCommand
    {
        get => (Command)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public object RemoveCommandParameter
    {
        get => GetValue(RemoveCommandParameterProperty);
        set => SetValue(RemoveCommandParameterProperty, value);
    }

    public Command AdvancedViewCommand
    {
        get => (Command)GetValue(AdvancedViewCommandProperty);
        set => SetValue(AdvancedViewCommandProperty, value);
    }

    public event EventHandler SelectionChanged;

    private void CollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectionChanged?.Invoke(this, e);
    }
}