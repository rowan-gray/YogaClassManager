using System.Windows.Input;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Views.Classes;

public partial class ClassDetailsView : ContentView
{
    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(ClassDetailsView));

    public static readonly BindableProperty ClassProperty =
        BindableProperty.Create(nameof(Class), typeof(ClassSchedule), typeof(ClassDetailsView));

    public ClassDetailsView()
    {
        InitializeComponent();
    }

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ClassSchedule Class
    {
        get => (ClassSchedule)GetValue(ClassProperty);
        set => SetValue(ClassProperty, value);
    }
}