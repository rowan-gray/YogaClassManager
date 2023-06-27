using System.Windows.Input;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Views.Classes;

public partial class ClassDetailsView : ContentView
{
    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(ClassDetailsView));

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(ClassDetailsView.EditCommandProperty);
        set => SetValue(ClassDetailsView.EditCommandProperty, value);
    }

    public static readonly BindableProperty ClassProperty = BindableProperty.Create(nameof(Class), typeof(ClassSchedule), typeof(ClassDetailsView));

    public ClassSchedule Class
    {
        get => (ClassSchedule)GetValue(ClassDetailsView.ClassProperty);
        set => SetValue(ClassDetailsView.ClassProperty, value);
    }

    public ClassDetailsView()
    {
        InitializeComponent();
    }
}