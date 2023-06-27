using System.Windows.Input;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Views.Terms;

public partial class TermDetailsView : ContentView
{
    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(TermDetailsView));

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(TermDetailsView.EditCommandProperty);
        set => SetValue(TermDetailsView.EditCommandProperty, value);
    }


    public static readonly BindableProperty TermProperty = BindableProperty.Create(nameof(Term), typeof(Term), typeof(TermDetailsView));

    public Term Term
    {
        get => (Term)GetValue(TermDetailsView.TermProperty);
        set => SetValue(TermDetailsView.TermProperty, value);
    }

    public TermDetailsView()
    {
        InitializeComponent();
    }
}