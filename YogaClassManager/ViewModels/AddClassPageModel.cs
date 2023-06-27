using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Classes;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(CallbackParameter), "parameter1")]
    public partial class AddClassPageModel : ObservableObject
    {
        public Message CallbackParameter { set => Callback = (Action<int>)value.Parameter; }

        public Action<int> Callback { get; set; }

        [ObservableProperty]
        private ClassSchedule classSchedule;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public AddClassPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            ClassSchedule = new(-1, DayOfWeek.Monday, TimeOnly.MinValue, false);
            AddCommand = new Command(AddCommandExecute);
            CancelCommand = new Command(CancelCommandExecute);
            this.databaseManager = databaseManager;
            this.popupService = popupService;
        }

        public async void AddCommandExecute()
        {
            int id;
            try
            {
                id = await databaseManager.ClassesService.AddClassAsync(CancellationToken.None, ClassSchedule);
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                return;
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                return;
            }
            await NavigationService.GoBackAsync();
            Callback?.Invoke(id);
        }

        public async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }

        public Command AddCommand { get; init; }

        public Command CancelCommand { get; init; }
        public List<DayOfWeek> Days => new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
    }
}
