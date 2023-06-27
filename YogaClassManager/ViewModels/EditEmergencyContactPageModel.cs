using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.People;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(EmergencyContactParameter), "emergencyContact")]
    [QueryProperty(nameof(VoidCallbackParameter), "voidCallback")]
    [QueryProperty(nameof(EmergencyContactCallbackParameter), "emergencyContactCallback")]
    [QueryProperty(nameof(SaveToDatabaseParameter), "saveToDatabase")]
    public partial class EditEmergencyContactPageModel : ObservableObject
    {
        public Message VoidCallbackParameter { set => VoidCallback = (Action)value.Parameter; }

        public Action VoidCallback { get; set; }
        public Message EmergencyContactCallbackParameter { set => EmergencyContactCallback = (Action<EmergencyContact>)value.Parameter; }

        public Action<EmergencyContact> EmergencyContactCallback { get; set; }
        public Message SaveToDatabaseParameter { set => SaveToDatabase = (bool)value.Parameter; }

        public bool SaveToDatabase { get; set; }
        public Message EmergencyContactParameter { set => EmergencyContact = (EmergencyContact)value.Parameter; }

        [ObservableProperty]
        private EmergencyContact emergencyContact;

        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public List<Relationship> Relationships { get; set; }

        public EditEmergencyContactPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            Relationships = new List<Relationship>() { Relationship.Parent, Relationship.Spouse, Relationship.Partner, Relationship.Child, Relationship.Friend, Relationship.Other };
            SaveCommand = new Command(SaveCommandExecute);
            CancelCommand = new Command(CancelCommandExecute);
            SaveToDatabase = true;
        }

        public Command SaveCommand { get; init; }

        public Command CancelCommand { get; init; }

        public async void SaveCommandExecute()
        {
            var success = true;

            if (SaveToDatabase)
            {
                success = await SaveEmergencyContactToDatabase(EmergencyContact);
            }

            if (!success)
                return;

            await NavigationService.GoBackAsync();
            VoidCallback?.Invoke();
            EmergencyContactCallback?.Invoke(EmergencyContact);
        }

        private async Task<bool> SaveEmergencyContactToDatabase(EmergencyContact emergencyContact)
        {
            try
            {
                await databaseManager.StudentsService.UpdateEmergencyContactAsync(CancellationToken.None, emergencyContact);
                return true;
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                return false;
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                return false;
            }
        }

        public async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }
    }
}
