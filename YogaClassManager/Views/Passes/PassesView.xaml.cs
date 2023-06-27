using YogaClassManager.Models.Passes;

namespace YogaClassManager.Views;

public partial class PassesView : ContentView
{
    public event EventHandler SelectionChanged;

    public static readonly BindableProperty PassesProperty = BindableProperty.Create(nameof(Passes), typeof(IEnumerable<Pass>), typeof(PassesView));

    public IEnumerable<Pass> Passes
    {
        get => (IEnumerable<Pass>)GetValue(PassesView.PassesProperty);
        set => SetValue(PassesView.PassesProperty, value);
    }

    public static readonly BindableProperty SelectedPassProperty = BindableProperty.Create(nameof(SelectedPass), typeof(Pass), typeof(PassesView));

    public Pass SelectedPass
    {
        get => (Pass)GetValue(PassesView.SelectedPassProperty);
        set => SetValue(PassesView.SelectedPassProperty, value);
    }

    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(Command), typeof(PassesView));

    public Command EditCommand
    {
        get => (Command)GetValue(PassesView.EditCommandProperty);
        set => SetValue(PassesView.EditCommandProperty, value);
    }

    public static readonly BindableProperty EditCommandParameterProperty = BindableProperty.Create(nameof(EditCommandParameter), typeof(object), typeof(PassesView));

    public object EditCommandParameter
    {
        get => GetValue(PassesView.EditCommandParameterProperty);
        set => SetValue(PassesView.EditCommandParameterProperty, value);
    }

    public static readonly BindableProperty AddCommandProperty = BindableProperty.Create(nameof(AddCommand), typeof(Command), typeof(PassesView));

    public Command AddCommand
    {
        get => (Command)GetValue(PassesView.AddCommandProperty);
        set => SetValue(PassesView.AddCommandProperty, value);
    }

    public static readonly BindableProperty AddCommandParameterProperty = BindableProperty.Create(nameof(AddCommandParameter), typeof(object), typeof(PassesView));

    public object AddCommandParameter
    {
        get => GetValue(PassesView.AddCommandParameterProperty);
        set => SetValue(PassesView.AddCommandParameterProperty, value);
    }

    public static readonly BindableProperty RemoveCommandProperty = BindableProperty.Create(nameof(RemoveCommand), typeof(Command), typeof(PassesView));

    public Command RemoveCommand
    {
        get => (Command)GetValue(PassesView.RemoveCommandProperty);
        set => SetValue(PassesView.RemoveCommandProperty, value);
    }

    public static readonly BindableProperty RemoveCommandParameterProperty = BindableProperty.Create(nameof(RemoveCommandParameter), typeof(object), typeof(PassesView));

    public object RemoveCommandParameter
    {
        get => GetValue(PassesView.RemoveCommandParameterProperty);
        set => SetValue(PassesView.RemoveCommandParameterProperty, value);
    }

    public static readonly BindableProperty AdvancedViewCommandProperty = BindableProperty.Create(nameof(AdvancedViewCommand), typeof(Command), typeof(PassesView));

    public Command AdvancedViewCommand
    {
        get => (Command)GetValue(PassesView.AdvancedViewCommandProperty);
        set => SetValue(PassesView.AdvancedViewCommandProperty, value);
    }

    public PassesView()
    {
        InitializeComponent();
    }

    private void CollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectionChanged?.Invoke(this, e);
    }
}