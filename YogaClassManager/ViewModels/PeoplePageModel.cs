using CommunityToolkit.Mvvm.Input;
using YogaClassManager.Database;
using YogaClassManager.Models.People;
using YogaClassManager.Services;
using YogaClassManager.ViewModels.Base;

namespace YogaClassManager.ViewModels
{
    public partial class PeoplePageModel : SearchableCollectionPageModel<Person>
    {
        private PeopleService peopleService => databaseManager.PeopleService;

        protected override void ChangeSelectedItem(Person item)
        {
            base.ChangeSelectedItem(item);
            RemovePersonCommand?.NotifyCanExecuteChanged();
        }


        private bool showUnusedPeople = false;

        public bool ShowInactivePeople
        {
            get => showUnusedPeople;
            set
            {
                showUnusedPeople = value;
                RetrieveCollection();
                OnPropertyChanged();
            }
        }

        public Command EditDetailsCommand { get; init; }
        public Command AddPersonCommand { get; init; }
        public RelayCommand RemovePersonCommand { get; init; }
        public Command UpdateRemovePersonCommandCanExecuteCommand { get; init; }

        public PeoplePageModel(DatabaseManager databaseManager, PopupService popupService) : base(databaseManager, popupService, 50)
        {
            EditDetailsCommand = new(EditDetailsCommandExecute);
            AddPersonCommand = new(AddPersonCommandExecute);
            RemovePersonCommand = new(RemovePeopleCommandExecute, RemovePeopleCommandCanExecute);
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
            return Selection is not null;
        }

        private async void AddPersonCommandExecute()
        {
            await NavigationService.NavigateToAddPersonPage(personReturn: new(PersonAdded));

        }

        private async void PersonAdded(Person addedPerson)
        {
            if (!retrievedCollection.Exists(person => person.Id == addedPerson.Id))
            {
                CurrentSearchQuery = addedPerson.FullName;
                await SearchCollection();
            }

            var person = retrievedCollection.FirstOrDefault(person => person.Id == addedPerson.Id);

            OnScrollToItem(person, false);
            Selection = person;
        }

        private async void EditDetailsCommandExecute()
        {
            //await NavigationService.NavigateTo(nameof(EditDetailsPage), Person.Copy(Selection));
        }

        protected override Task<List<Person>> RetrieveSearchedCollection(string query)
        {
                return peopleService.SearchPeopleAsync(cancellationToken.Token, query, returnHiddenPeople: ShowInactivePeople);
        }
        protected override Task<List<Person>> RetrieveUnsearchedCollection()
        {
                return peopleService.GetPeopleAsync(cancellationToken.Token, ShowInactivePeople);
        }

        protected override Task<List<Person>> GetSearchedUpdatedItems(string query)
        {
                return peopleService.GetUpdatedPeopleMatchingQuery(cancellationToken.Token, timeLastUpdated, query, ShowInactivePeople);
        }

        protected override Task<List<Person>> GetUnsearchedUpdatedItems()
        {
            return peopleService.GetUpdatedPeople(cancellationToken.Token, timeLastUpdated, ShowInactivePeople);
        }

        protected override List<Person> SortCollection(List<Person> collection)
        {
            return collection.OrderBy(person => person.FullName).ToList();
        }

        protected override Task<List<int>> GetDeletedIds()
        {
            return peopleService.GetDeletedPeopleIds(cancellationToken.Token, timeLastUpdated);
        }
    }
}
