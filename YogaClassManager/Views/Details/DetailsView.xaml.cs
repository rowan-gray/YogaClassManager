using System.Windows.Input;
using YogaClassManager.Models.People;

namespace YogaClassManager.Views;

public partial class DetailsView : ContentView
{
    public static readonly BindableProperty PersonProperty = BindableProperty.Create(nameof(Person), typeof(Person), typeof(DetailsView));

    public Person Person
    {
        get => (Person)GetValue(DetailsView.PersonProperty);
        set => SetValue(DetailsView.PersonProperty, value);
    }

    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(DetailsView));

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(DetailsView.EditCommandProperty);
        set => SetValue(DetailsView.EditCommandProperty, value);
    }

    public static readonly BindableProperty EditCommandParameterProperty = BindableProperty.Create(nameof(EditCommandParameter), typeof(object), typeof(DetailsView));

    public object EditCommandParameter
    {
        get => GetValue(DetailsView.EditCommandParameterProperty);
        set => SetValue(DetailsView.EditCommandParameterProperty, value);
    }

    public DetailsView()
    {
        InitializeComponent();
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        EditCommand.Execute(EditCommandParameter);
    }
}