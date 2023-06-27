using System.Windows.Input;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Views.Classes;

public partial class ClassRollsView : ContentView
{
    public ClassRollsView()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty ClassRollsProperty = BindableProperty.Create(nameof(ClassRolls), typeof(IEnumerable<ClassRoll>), typeof(ClassRollsView));

    public IEnumerable<ClassRoll> ClassRolls
    {
        get => (IEnumerable<ClassRoll>)GetValue(ClassRollsView.ClassRollsProperty);
        set => SetValue(ClassRollsView.ClassRollsProperty, value);
    }

    public static readonly BindableProperty SelectedClassRollProperty = BindableProperty.Create(nameof(SelectedClassRoll), typeof(ClassRoll), typeof(ClassRollsView));

    public ClassRoll SelectedClassRoll
    {
        get => (ClassRoll)GetValue(ClassRollsView.SelectedClassRollProperty);
        set => SetValue(ClassRollsView.SelectedClassRollProperty, value);
    }

    public static readonly BindableProperty AddCommandProperty = BindableProperty.Create(nameof(AddCommand), typeof(ICommand), typeof(ClassRollsView));

    public ICommand AddCommand
    {
        get => (ICommand)GetValue(ClassRollsView.AddCommandProperty);
        set => SetValue(ClassRollsView.AddCommandProperty, value);
    }

    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(ClassRollsView));

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(ClassRollsView.EditCommandProperty);
        set => SetValue(ClassRollsView.EditCommandProperty, value);
    }

    public static readonly BindableProperty RemoveCommandProperty = BindableProperty.Create(nameof(RemoveCommand), typeof(ICommand), typeof(ClassRollsView));

    public ICommand RemoveCommand
    {
        get => (ICommand)GetValue(ClassRollsView.RemoveCommandProperty);
        set => SetValue(ClassRollsView.RemoveCommandProperty, value);
    }
}