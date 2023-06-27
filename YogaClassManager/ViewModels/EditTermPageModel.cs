using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Classes;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(TermParameter), "term")]
    [QueryProperty(nameof(CallbackParameter), "callback")]
    public partial class EditTermPageModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCatchupStartDate))]
        [NotifyPropertyChangedFor(nameof(IsCatchupEndDate))]
        private Term term;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public EditTermPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            SaveCommand = new Command(SaveCommandExecute, () => Term is not null ? Term.IsValid() : false);
            CancelCommand = new Command(CancelCommandExecute);
            UpdateSaveCommand = new Command(() => SaveCommand.ChangeCanExecute());
        }
        public bool IsCatchupStartDate
        {
            get => Term?.CatchupStartDate is not null;
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
            get => Term?.CatchupEndDate is not null;
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

        public async void SaveCommandExecute()
        {
            try
            {
                await databaseManager.TermsService.UpdateTermAsync(CancellationToken.None, Term);
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
        public Command UpdateSaveCommand { get; init; }

        public Command CancelCommand { get; init; }

        public Message TermParameter { set => Term = (Term)value.Parameter; }
        public Message CallbackParameter { set => Callback = (Action)value.Parameter; }

        public Action Callback { get; set; }
    }
}
