using YogaClassManager.Models.People;

namespace YogaClassManager.Views;

public partial class EmergencyContactsView : ContentView
{
    public static readonly BindableProperty EmergencyContactsProperty =
        BindableProperty.Create(nameof(EmergencyContacts), typeof(IEnumerable<EmergencyContact>),
            typeof(EmergencyContactsView));

    public static readonly BindableProperty AddCommandProperty =
        BindableProperty.Create(nameof(AddCommand), typeof(Command), typeof(EmergencyContactsView));

    public static readonly BindableProperty AddCommandParameterProperty =
        BindableProperty.Create(nameof(AddCommandParameter), typeof(object), typeof(EmergencyContactsView));

    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(Command), typeof(EmergencyContactsView));

    public static readonly BindableProperty EditCommandParameterProperty =
        BindableProperty.Create(nameof(EditCommandParameter), typeof(object), typeof(EmergencyContactsView));

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(Command), typeof(EmergencyContactsView));

    public static readonly BindableProperty RemoveCommandParameterProperty =
        BindableProperty.Create(nameof(RemoveCommandParameter), typeof(object), typeof(EmergencyContactsView));

    public static readonly BindableProperty SelectedEmergencyContactProperty =
        BindableProperty.Create(nameof(SelectedEmergencyContact), typeof(EmergencyContact),
            typeof(EmergencyContactsView));

    public EmergencyContactsView()
    {
        InitializeComponent();
    }

    public IEnumerable<EmergencyContact> EmergencyContacts
    {
        get => (IEnumerable<EmergencyContact>)GetValue(EmergencyContactsProperty);
        set => SetValue(EmergencyContactsProperty, value);
    }

    public Command? AddCommand
    {
        get => (Command?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public object AddCommandParameter
    {
        get => GetValue(AddCommandParameterProperty);
        set => SetValue(AddCommandParameterProperty, value);
    }

    public Command? EditCommand
    {
        get => (Command?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public object EditCommandParameter
    {
        get => GetValue(EditCommandParameterProperty);
        set => SetValue(EditCommandParameterProperty, value);
    }

    public Command? RemoveCommand
    {
        get => (Command?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public object RemoveCommandParameter
    {
        get => GetValue(RemoveCommandParameterProperty);
        set => SetValue(RemoveCommandParameterProperty, value);
    }

    public EmergencyContact SelectedEmergencyContact
    {
        get => (EmergencyContact)GetValue(SelectedEmergencyContactProperty);
        set => SetValue(SelectedEmergencyContactProperty, value);
    }
}