using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YogaClassManager.Database;
using YogaClassManager.NewModels.People;
using YogaClassManager.NewDatabase.People;
using YogaClassManager.NewModels;
using YogaClassManager.Services;
using YogaClassManager.ViewModels.Base;
using DatabaseService = YogaClassManager.NewDatabase.DatabaseService;

namespace YogaClassManager.ViewModels;

public partial class PeoplePageModel : BasePageModel
{
    private readonly DatabaseService databaseService;
    private readonly PopupService popupService;
    private bool showUnusedPeople;
    [ObservableProperty] private Person? selectedPerson = null;
    [ObservableProperty] private GrowableDbCollection<PersonDbModel, Person, PersonFilter> people;
    private PersonDbModel dbModel;

    public PeoplePageModel(DatabaseService databaseService, PopupService popupService)
    {
        dbModel = new PersonDbModel(new PersonFilter(), databaseService);

        People = new GrowableDbCollection<PersonDbModel, Person, PersonFilter>(dbModel);
        people.Add(new Person(-1, "Rowan", "Gray", "0478570039",null, true));
        people.Add(new Person());
        people.Add(new Person());
        
        this.databaseService = databaseService;
        this.popupService = popupService;
        EditDetailsCommand = new Command(EditDetailsCommandExecute);
        AddPersonCommand = new Command(AddPersonCommandExecute);
        RemovePersonCommand = new RelayCommand(RemovePeopleCommandExecute, RemovePeopleCommandCanExecute);
        
        LoadData();
    }

    private async void LoadData()
    {
        await People.GrowCollection(20);
    }
    
    public bool ShowInactivePeople
    {
        get => showUnusedPeople;
        set
        {
            showUnusedPeople = value;
            //RetrieveCollection();
            OnPropertyChanged();
        }
    }

    public Command EditDetailsCommand { get; init; }
    public Command AddPersonCommand { get; init; }
    public RelayCommand RemovePersonCommand { get; init; }
    public Command UpdateRemovePersonCommandCanExecuteCommand { get; init; }

    protected void ChangeSelectedItem(Person item)
    {
        // base.ChangeSelectedItem(item);
        RemovePersonCommand?.NotifyCanExecuteChanged();
    }


    private void RemovePeopleCommandExecute()
    {
        //if (SelectedPerson is null)
        //    return;

        //IsBusy = true;
        //if (SelectedPerson.IsActive)
        //{
        //    try
        //    {
        //        await databaseManager.PeopleService.HidePersonAsync(cancellationToken.Token, SelectedPerson);
        //        if (!ShowInactiveStudents)
        //        {
        //            Students.Remove(SelectedPerson);
        //            SelectedPerson = Students.FirstOrDefault();
        //        }
        //    }
        //    catch (TaskCanceledException)
        //    {
        //        await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
        //    }
        //}
        //else
        //{
        //    try
        //    {
        //        await databaseManager.PeopleService.UnhidePersonAsync(cancellationToken.Token, SelectedPerson);
        //    }
        //    catch (TaskCanceledException)
        //    {
        //        await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
        //    }
        //}
        //IsBusy = false;

        //UpdateStudents();
    }

    private bool RemovePeopleCommandCanExecute()
    {
        return SelectedPerson is not null;
    }

    private async void AddPersonCommandExecute()
    {
        //await NavigationService.NavigateToAddPersonPage(personReturn: PersonAdded);
    }

    private async void PersonAdded(Person addedPerson)
    {
        // if (!retrievedCollection.Exists(person => person.Id == addedPerson.Id))
        // {
        //     CurrentSearchQuery = addedPerson.FullName;
        //     await SearchCollection();
        // }
        //
        // var person = retrievedCollection.FirstOrDefault(person => person.Id == addedPerson.Id);
        //
        // OnScrollToItem(person, false);
        // Selection = person;
    }

    private async void EditDetailsCommandExecute()
    {
        //await NavigationService.NavigateTo(nameof(EditDetailsPage), Person.Copy(Selection));
    }
}