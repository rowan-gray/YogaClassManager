using System.Windows.Input;
using YogaClassManager.Models.People;

namespace YogaClassManager.Views;

public partial class DetailsView : ContentView
{
    public static readonly BindableProperty PersonProperty =
        BindableProperty.Create(nameof(Person), typeof(Person), typeof(DetailsView));

    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(DetailsView));

    public static readonly BindableProperty EditCommandParameterProperty =
        BindableProperty.Create(nameof(EditCommandParameter), typeof(object), typeof(DetailsView));

    public DetailsView()
    {
        InitializeComponent();
    }

    public Person Person
    {
        get => (Person)GetValue(PersonProperty);
        set => SetValue(PersonProperty, value);
    }

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public object EditCommandParameter
    {
        get => GetValue(EditCommandParameterProperty);
        set => SetValue(EditCommandParameterProperty, value);
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        EditCommand.Execute(EditCommandParameter);
    }
}