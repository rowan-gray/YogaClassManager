using YogaClassManager.Models.People;

namespace YogaClassManager.Views;

public partial class EmergencyContactsView : ContentView
{
    public static readonly BindableProperty EmergencyContactsProperty = BindableProperty.Create(nameof(EmergencyContacts), typeof(IEnumerable<EmergencyContact>), typeof(EmergencyContactsView));

    public IEnumerable<EmergencyContact> EmergencyContacts
    {
        get => (IEnumerable<EmergencyContact>)GetValue(EmergencyContactsView.EmergencyContactsProperty);
        set => SetValue(EmergencyContactsView.EmergencyContactsProperty, value);
    }

    public static readonly BindableProperty AddCommandProperty = BindableProperty.Create(nameof(AddCommand), typeof(Command), typeof(EmergencyContactsView));

    public Command AddCommand
    {
        get => (Command)GetValue(EmergencyContactsView.AddCommandProperty);
        set => SetValue(EmergencyContactsView.AddCommandProperty, value);
    }

    public static readonly BindableProperty AddCommandParameterProperty = BindableProperty.Create(nameof(AddCommandParameter), typeof(object), typeof(EmergencyContactsView));

    public object AddCommandParameter
    {
        get => GetValue(EmergencyContactsView.AddCommandParameterProperty);
        set => SetValue(EmergencyContactsView.AddCommandParameterProperty, value);
    }

    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(Command), typeof(EmergencyContactsView));

    public Command EditCommand
    {
        get => (Command)GetValue(EmergencyContactsView.EditCommandProperty);
        set => SetValue(EmergencyContactsView.EditCommandProperty, value);
    }

    public static readonly BindableProperty EditCommandParameterProperty = BindableProperty.Create(nameof(EditCommandParameter), typeof(object), typeof(EmergencyContactsView));

    public object EditCommandParameter
    {
        get => GetValue(EmergencyContactsView.EditCommandParameterProperty);
        set => SetValue(EmergencyContactsView.EditCommandParameterProperty, value);
    }

    public static readonly BindableProperty RemoveCommandProperty = BindableProperty.Create(nameof(RemoveCommand), typeof(Command), typeof(EmergencyContactsView));

    public Command RemoveCommand
    {
        get => (Command)GetValue(EmergencyContactsView.RemoveCommandProperty);
        set => SetValue(EmergencyContactsView.RemoveCommandProperty, value);
    }

    public static readonly BindableProperty RemoveCommandParameterProperty = BindableProperty.Create(nameof(RemoveCommandParameter), typeof(object), typeof(EmergencyContactsView));

    public object RemoveCommandParameter
    {
        get => GetValue(EmergencyContactsView.RemoveCommandParameterProperty);
        set => SetValue(EmergencyContactsView.RemoveCommandParameterProperty, value);
    }

    public static readonly BindableProperty SelectedEmergencyContactProperty = BindableProperty.Create(nameof(SelectedEmergencyContact), typeof(EmergencyContact), typeof(EmergencyContactsView));

    public EmergencyContact SelectedEmergencyContact
    {
        get => (EmergencyContact)GetValue(EmergencyContactsView.SelectedEmergencyContactProperty);
        set => SetValue(EmergencyContactsView.SelectedEmergencyContactProperty, value);
    }

    public EmergencyContactsView()
    {
        InitializeComponent();
    }

    private void AddButtonClicked(object sender, EventArgs e)
    {
        AddCommand.Execute(AddCommandParameter);
    }

    private void EditButtonClicked(object sender, EventArgs e)
    {
        EditCommand.Execute(EditCommandParameter);
    }

    private void RemoveButtonClicked(object sender, EventArgs e)
    {
        RemoveCommand.Execute(RemoveCommandParameter);
    }
}