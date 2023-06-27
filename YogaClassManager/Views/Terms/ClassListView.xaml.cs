using YogaClassManager.Models.Classes;

namespace YogaClassManager.Views.Terms;

public partial class ClassListView : ContentView
{
    public ClassListView()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty ClassesProperty = BindableProperty.Create(nameof(Classes), typeof(IEnumerable<TermClassSchedule>), typeof(ClassListView));

    public IEnumerable<TermClassSchedule> Classes
    {
        get => (IEnumerable<TermClassSchedule>)GetValue(ClassListView.ClassesProperty);
        set => SetValue(ClassListView.ClassesProperty, value);
    }

    public static readonly BindableProperty SelectedClassProperty = BindableProperty.Create(nameof(SelectedClass), typeof(TermClassSchedule), typeof(ClassListView));

    public TermClassSchedule SelectedClass
    {
        get => (TermClassSchedule)GetValue(ClassListView.SelectedClassProperty);
        set => SetValue(ClassListView.SelectedClassProperty, value);
    }

    public static readonly BindableProperty AddCommandProperty = BindableProperty.Create(nameof(AddCommand), typeof(Command), typeof(ClassListView));

    public Command AddCommand
    {
        get => (Command)GetValue(ClassListView.AddCommandProperty);
        set => SetValue(ClassListView.AddCommandProperty, value);
    }

    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(Command), typeof(ClassListView));

    public Command EditCommand
    {
        get => (Command)GetValue(ClassListView.EditCommandProperty);
        set => SetValue(ClassListView.EditCommandProperty, value);
    }

    public static readonly BindableProperty RemoveCommandProperty = BindableProperty.Create(nameof(RemoveCommand), typeof(Command), typeof(ClassListView));

    public Command RemoveCommand
    {
        get => (Command)GetValue(ClassListView.RemoveCommandProperty);
        set => SetValue(ClassListView.RemoveCommandProperty, value);
    }
}