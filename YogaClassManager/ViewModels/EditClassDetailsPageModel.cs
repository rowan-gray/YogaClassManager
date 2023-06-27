using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Classes;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(ClassParameter), "classSchedule")]
    [QueryProperty(nameof(CallbackParameter), "callbackParameter")]
    public partial class EditClassDetailsPageModel : ObservableObject
    {
        [ObservableProperty]
        private ClassSchedule classSchedule;
        [ObservableProperty]
        private List<ClassGroup> classGroups;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public EditClassDetailsPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            SaveCommand = new Command(SaveCommandExecute);
            CancelCommand = new Command(CancelCommandExecute);
        }

        public async void SaveCommandExecute()
        {
            try
            {
                await databaseManager.ClassesService.UpdateClassAsync(CancellationToken.None, ClassSchedule);
                await NavigationService.GoBackAsync();
                Callback?.Invoke();
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

        public async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }


        public Command SaveCommand { get; init; }

        public Command CancelCommand { get; init; }

        public Message ClassParameter { set => ClassSchedule = (ClassSchedule)value.Parameter; }
        public Message CallbackParameter { set => Callback = (Action)value.Parameter; }

        public Action Callback { get; set; }

        public List<DayOfWeek> Days => new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

    }
}
