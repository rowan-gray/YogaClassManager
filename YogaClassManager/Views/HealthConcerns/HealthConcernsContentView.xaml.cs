namespace YogaClassManager.Views;

public partial class HealthConcernsContentView : ContentView
{
    public static readonly BindableProperty SelectedHealthConcernProperty = BindableProperty.Create(nameof(SelectedHealthConcern), typeof(string), typeof(HealthConcernsContentView), null, BindingMode.TwoWay);

    public string SelectedHealthConcern
    {
        get => (string)GetValue(HealthConcernsContentView.SelectedHealthConcernProperty);
        set => SetValue(HealthConcernsContentView.SelectedHealthConcernProperty, value);
    }
    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(Command), typeof(HealthConcernsContentView));

    public Command EditCommand
    {
        get => (Command)GetValue(HealthConcernsContentView.EditCommandProperty);
        set => SetValue(HealthConcernsContentView.EditCommandProperty, value);
    }

    public static readonly BindableProperty EditCommandParameterProperty = BindableProperty.Create(nameof(EditCommandParameter), typeof(object), typeof(HealthConcernsContentView));

    public object EditCommandParameter
    {
        get => GetValue(HealthConcernsContentView.EditCommandParameterProperty);
        set => SetValue(HealthConcernsContentView.EditCommandParameterProperty, value);
    }

    public static readonly BindableProperty AddCommandProperty = BindableProperty.Create(nameof(AddCommand), typeof(Command), typeof(HealthConcernsContentView));

    public Command AddCommand
    {
        get => (Command)GetValue(HealthConcernsContentView.AddCommandProperty);
        set => SetValue(HealthConcernsContentView.AddCommandProperty, value);
    }

    public static readonly BindableProperty AddCommandParameterProperty = BindableProperty.Create(nameof(AddCommandParameter), typeof(object), typeof(HealthConcernsContentView));

    public object AddCommandParameter
    {
        get => GetValue(HealthConcernsContentView.AddCommandParameterProperty);
        set => SetValue(HealthConcernsContentView.AddCommandParameterProperty, value);
    }

    public static readonly BindableProperty RemoveCommandProperty = BindableProperty.Create(nameof(RemoveCommand), typeof(Command), typeof(HealthConcernsContentView));

    public Command RemoveCommand
    {
        get => (Command)GetValue(HealthConcernsContentView.RemoveCommandProperty);
        set => SetValue(HealthConcernsContentView.RemoveCommandProperty, value);
    }

    public static readonly BindableProperty RemoveCommandParameterProperty = BindableProperty.Create(nameof(RemoveCommandParameter), typeof(object), typeof(HealthConcernsContentView));

    public object RemoveCommandParameter
    {
        get => GetValue(HealthConcernsContentView.RemoveCommandParameterProperty);
        set => SetValue(HealthConcernsContentView.RemoveCommandParameterProperty, value);
    }

    public static readonly BindableProperty HealthConcernsProperty = BindableProperty.Create(nameof(HealthConcerns), typeof(IEnumerable<string>), typeof(HealthConcernsContentView));

    public IEnumerable<string> HealthConcerns
    {
        get => (IEnumerable<string>)GetValue(HealthConcernsContentView.HealthConcernsProperty);
        set => SetValue(HealthConcernsContentView.HealthConcernsProperty, value);
    }

    public HealthConcernsContentView()
    {
        InitializeComponent();
    }
}