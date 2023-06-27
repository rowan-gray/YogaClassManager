using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Classes;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(CallbackParameter), "parameter1")]
    public partial class AddTermPageModel : ObservableObject
    {
        [ObservableProperty]
        private Term term;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public bool IsCatchupStartDate
        {
            get => Term.CatchupStartDate is not null;
            set
            {
                OnPropertyChanging();

                if (value == false)
                {
                    Term.CatchupStartDate = null;
                }
                else
                {
                    Term.CatchupStartDate = Term.StartDate.AddDays(-7);
                }

                OnPropertyChanged();
            }
        }
        public bool IsCatchupEndDate
        {
            get => Term.CatchupEndDate is not null;
            set
            {
                OnPropertyChanging();
                if (value == false)
                {
                    Term.CatchupEndDate = null;
                }
                else
                {
                    Term.CatchupEndDate = Term.EndDate.AddDays(7);
                }
                OnPropertyChanged();
            }
        }

        public AddTermPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            Term = new Term(-1, null, DateOnly.FromDateTime(DateTime.Now), DateOnly.FromDateTime(DateTime.Now.AddDays(7 * 10)), null, null, new());
            AddCommand = new Command(AddCommandExecute, AddCommandCanExecute);
            CancelCommand = new Command(CancelCommandExecute);
            UpdateAddCommand = new Command(() => AddCommand.ChangeCanExecute());
        }

        public async void AddCommandExecute()
        {
            try
            {
                var id = await databaseManager.TermsService.AddTermAsync(CancellationToken.None, Term);
                await NavigationService.GoBackAsync();
                Callback?.Invoke(id);
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

        public bool AddCommandCanExecute()
        {
            return Term.IsValid();
        }

        public async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }

        public Command AddCommand { get; init; }
        public Command UpdateAddCommand { get; init; }

        public Command CancelCommand { get; init; }

        public Message CallbackParameter { set => Callback = (Action<int>)value.Parameter; }

        public Action<int> Callback { get; set; }
    }
}
