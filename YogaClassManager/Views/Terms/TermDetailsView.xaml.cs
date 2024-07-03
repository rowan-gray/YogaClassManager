using System.Windows.Input;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Views.Terms;

public partial class TermDetailsView : ContentView
{
    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(TermDetailsView));


    public static readonly BindableProperty TermProperty =
        BindableProperty.Create(nameof(Term), typeof(Term), typeof(TermDetailsView));

    public TermDetailsView()
    {
        InitializeComponent();
    }

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public Term Term
    {
        get => (Term)GetValue(TermProperty);
        set => SetValue(TermProperty, value);
    }
}