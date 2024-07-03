using YogaClassManager.Models.Classes;

namespace YogaClassManager.Views.Terms;

public partial class ClassListView : ContentView
{
    public static readonly BindableProperty ClassesProperty = BindableProperty.Create(nameof(Classes),
        typeof(IEnumerable<TermClassSchedule>), typeof(ClassListView));

    public static readonly BindableProperty SelectedClassProperty =
        BindableProperty.Create(nameof(SelectedClass), typeof(TermClassSchedule), typeof(ClassListView));

    public static readonly BindableProperty AddCommandProperty =
        BindableProperty.Create(nameof(AddCommand), typeof(Command), typeof(ClassListView));

    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(Command), typeof(ClassListView));

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(Command), typeof(ClassListView));

    public ClassListView()
    {
        InitializeComponent();
    }

    public IEnumerable<TermClassSchedule> Classes
    {
        get => (IEnumerable<TermClassSchedule>)GetValue(ClassesProperty);
        set => SetValue(ClassesProperty, value);
    }

    public TermClassSchedule SelectedClass
    {
        get => (TermClassSchedule)GetValue(SelectedClassProperty);
        set => SetValue(SelectedClassProperty, value);
    }

    public Command AddCommand
    {
        get => (Command)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public Command EditCommand
    {
        get => (Command)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public Command RemoveCommand
    {
        get => (Command)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }
}