using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.People;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(PersonParameter), "parameter1")]
    [QueryProperty(nameof(CallbackParameter), "parameter2")]
    public partial class EditDetailsPageModel : ObservableObject
    {
        [ObservableProperty]
        private Person person;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public Message PersonParameter { set => Person = (Person)value.Parameter; }
        public Message CallbackParameter { set => Callback = (Action<Person>)value.Parameter; }

        public Action<Person> Callback { get; set; }

        public Command SaveCommand { get; init; }
        public Command CancelCommand { get; init; }
        public Command UpdateCanExecutesCommand { get; init; }

        public EditDetailsPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            SaveCommand = new Command(SaveCommandExecute, SaveCommandCanExecute);
            CancelCommand = new Command(CancelCommandExecute);
            UpdateCanExecutesCommand = new Command(UpdateCanExecutesCommandExecute);
            this.databaseManager = databaseManager;
            this.popupService = popupService;
        }

        private bool SaveCommandCanExecute()
        {
            if (Person is null)
                return false;

            if (Person is not Student)
            {
                return Person.Validate();
            }
            else
            {
                return ((Student)Person).Validate();
            }
        }
        public void UpdateCanExecutesCommandExecute()
        {
            SaveCommand?.ChangeCanExecute();
        }

        private async void SaveCommandExecute()
        {
            try
            {
                await databaseManager.PeopleService.SavePersonAsync(CancellationToken.None, Person);
                await NavigationService.GoBackAsync();
                Callback?.Invoke(Person);
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }
            
        }
        private async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }
    }
}
