using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.People;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels;

[QueryProperty(nameof(StudentParameter), "student")]
[QueryProperty(nameof(VoidCallbackParameter), "voidCallback")]
[QueryProperty(nameof(EmergencyContactCallbackParameter), "emergencyContactCallback")]
[QueryProperty(nameof(SaveToDatabaseParameter), "saveToDatabase")]
public partial class AddEmergencyContactPageModel : ObservableObject
{
    private readonly DatabaseManager databaseManager;
    private readonly PopupService popupService;

    [ObservableProperty] private Person selectedPerson;

    [ObservableProperty] private Relationship selectedRelationship;

    [ObservableProperty] private Student student;

    public AddEmergencyContactPageModel(DatabaseManager databaseManager, PopupService popupService)
    {
        this.databaseManager = databaseManager;
        this.popupService = popupService;
        Relationships = new List<Relationship>
        {
            Relationship.Parent, Relationship.Spouse, Relationship.Partner, Relationship.Child, Relationship.Friend,
            Relationship.Other
        };
        SaveCommand = new Command(SaveCommandExecute);
        CancelCommand = new Command(CancelCommandExecute);
        CreateNewPersonCommand = new Command(CreateNewPersonCommandExecute);
        RemovePersonCommand = new Command(RemovePersonCommandExecute);
        SearchPeopleCommand = new Command(SearchPeopleCommandExecute);
        SaveToDatabase = true;
    }

    public Message VoidCallbackParameter
    {
        set => VoidCallBack = (Action)value.Parameter;
    }

    public Action VoidCallBack { get; set; }

    public Message EmergencyContactCallbackParameter
    {
        set => EmergencyContactCallback = (Action<EmergencyContact>)value.Parameter;
    }

    public Action<EmergencyContact> EmergencyContactCallback { get; set; }

    public Message SaveToDatabaseParameter
    {
        set => SaveToDatabase = (bool)value.Parameter;
    }

    public bool SaveToDatabase { get; set; }

    public Message StudentParameter
    {
        set => Student = (Student)value.Parameter;
    }

    public List<Relationship> Relationships { get; set; }
    public Command SaveCommand { get; init; }

    public Command CancelCommand { get; init; }

    public Command CreateNewPersonCommand { get; init; }

    public Command SearchPeopleCommand { get; init; }

    public Command RemovePersonCommand { get; init; }

    private async void SearchPeopleCommandExecute(object obj)
    {
        await NavigationService.NavigateTo(nameof(SearchPeoplePageModel), new Action<Person>(SetPerson));
    }

    private void RemovePersonCommandExecute()
    {
        SelectedPerson = null;
    }

    private async void CreateNewPersonCommandExecute()
    {
        await NavigationService.NavigateToAddPersonPage(personReturn: SetPerson, saveToDatabase: false);
    }

    private void SetPerson(Person person)
    {
        SelectedPerson = person;
    }

    public async void SaveCommandExecute()
    {
        EmergencyContact emergencyContact;
        if (SaveToDatabase)
        {
            emergencyContact = new EmergencyContact(SelectedPerson, Student.Id, selectedRelationship);

            try
            {
                await databaseManager.StudentsService.AddEmergencyContactAsync(CancellationToken.None,
                    emergencyContact);
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error",
                    $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }
        }
        else
        {
            emergencyContact = new EmergencyContact(SelectedPerson, Student.Id, selectedRelationship);
        }

        await NavigationService.GoBackAsync();
        VoidCallBack?.Invoke();
        EmergencyContactCallback?.Invoke(emergencyContact);
    }

    public async void CancelCommandExecute()
    {
        await NavigationService.GoBackAsync();
    }
}