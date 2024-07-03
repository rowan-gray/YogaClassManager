namespace YogaClassManager.Views;

public partial class HealthConcernsContentView : ContentView
{
    public static readonly BindableProperty SelectedHealthConcernProperty =
        BindableProperty.Create(nameof(SelectedHealthConcern), typeof(string), typeof(HealthConcernsContentView), null,
            BindingMode.TwoWay);

    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(Command), typeof(HealthConcernsContentView));

    public static readonly BindableProperty EditCommandParameterProperty =
        BindableProperty.Create(nameof(EditCommandParameter), typeof(object), typeof(HealthConcernsContentView));

    public static readonly BindableProperty AddCommandProperty =
        BindableProperty.Create(nameof(AddCommand), typeof(Command), typeof(HealthConcernsContentView));

    public static readonly BindableProperty AddCommandParameterProperty =
        BindableProperty.Create(nameof(AddCommandParameter), typeof(object), typeof(HealthConcernsContentView));

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(Command), typeof(HealthConcernsContentView));

    public static readonly BindableProperty RemoveCommandParameterProperty =
        BindableProperty.Create(nameof(RemoveCommandParameter), typeof(object), typeof(HealthConcernsContentView));

    public static readonly BindableProperty HealthConcernsProperty = BindableProperty.Create(nameof(HealthConcerns),
        typeof(IEnumerable<string>), typeof(HealthConcernsContentView));

    public HealthConcernsContentView()
    {
        InitializeComponent();
    }

    public string SelectedHealthConcern
    {
        get => (string)GetValue(SelectedHealthConcernProperty);
        set => SetValue(SelectedHealthConcernProperty, value);
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

    public IEnumerable<string> HealthConcerns
    {
        get => (IEnumerable<string>)GetValue(HealthConcernsProperty);
        set => SetValue(HealthConcernsProperty, value);
    }
}