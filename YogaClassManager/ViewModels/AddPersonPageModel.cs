using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.People;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(IdCallbackParameter), "idCallback")]
    [QueryProperty(nameof(PersonCallbackParameter), "personCallback")]
    [QueryProperty(nameof(IsPersonSavedParameter), "saveToDatabase")]
    public partial class AddPersonPageModel : ObservableObject
    {
        public Message IdCallbackParameter { set => IdCallback = (Action<int>)value.Parameter; }

        public Action<int> IdCallback { get; set; }
        public Message PersonCallbackParameter { set => PersonCallback = (Action<Person>)value.Parameter; }

        public Action<Person> PersonCallback { get; set; }
        public Message IsPersonSavedParameter { set => IsPersonSaved = (bool)value.Parameter; }
        public bool IsPersonSaved { get; set; }

        public Command CancelCommand { get; set; }
        public Command AddCommand { get; set; }
        public Command UpdateCanExecutesCommand { get; init; }

        [ObservableProperty]
        private Person person;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public AddPersonPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            Person = new Person(-1, "", null, null, null, true);

            CancelCommand = new(CancelCommandExecute);
            AddCommand = new(AddCommandExecute, AddCommandCanExecute);
            UpdateCanExecutesCommand = new(UpdateCanExecutesCommandExecute);
        }

        private bool AddCommandCanExecute()
        {
            return person.Validate();
        }
        public void UpdateCanExecutesCommandExecute()
        {
            AddCommand?.ChangeCanExecute();
        }

        private async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }

        private async void AddCommandExecute()
        {
            var id = -1;

            var student = new Student(Person, new(), new(), new());

            if (!Person.Validate())
            {
                await popupService.DisplayAlert("Invalid field/s", "One or more of the fields is not valid!", "Ok");
                return;
            }

            if (IsPersonSaved)
            {
                try
                {
                    id = await databaseManager.PeopleService.AddPersonAsync(CancellationToken.None, Person);
                    await NavigationService.GoBackAsync();
                    IdCallback?.Invoke(id);
                    Person.Id = id;
                }
                catch (TaskCanceledException)
                {
                    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                    return;
                }
                catch (Exception e)
                {
                    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                }
            }
            else
            {
                await NavigationService.GoBackAsync();
            }

            PersonCallback?.Invoke(Person);

        }
    }
}
