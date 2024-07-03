using System.Windows.Input;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Views.Classes;

public partial class ClassRollsView : ContentView
{
    public static readonly BindableProperty ClassRollsProperty =
        BindableProperty.Create(nameof(ClassRolls), typeof(IEnumerable<ClassRoll>), typeof(ClassRollsView));

    public static readonly BindableProperty SelectedClassRollProperty =
        BindableProperty.Create(nameof(SelectedClassRoll), typeof(ClassRoll), typeof(ClassRollsView));

    public static readonly BindableProperty AddCommandProperty =
        BindableProperty.Create(nameof(AddCommand), typeof(ICommand), typeof(ClassRollsView));

    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(ClassRollsView));

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(ICommand), typeof(ClassRollsView));

    public ClassRollsView()
    {
        InitializeComponent();
    }

    public IEnumerable<ClassRoll> ClassRolls
    {
        get => (IEnumerable<ClassRoll>)GetValue(ClassRollsProperty);
        set => SetValue(ClassRollsProperty, value);
    }

    public ClassRoll SelectedClassRoll
    {
        get => (ClassRoll)GetValue(SelectedClassRollProperty);
        set => SetValue(SelectedClassRollProperty, value);
    }

    public ICommand AddCommand
    {
        get => (ICommand)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand RemoveCommand
    {
        get => (ICommand)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }
}