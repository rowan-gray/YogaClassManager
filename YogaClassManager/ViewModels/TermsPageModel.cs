using YogaClassManager.Database;
using YogaClassManager.Models.Classes;
using YogaClassManager.Services;
using YogaClassManager.ViewModels.Base;

namespace YogaClassManager.ViewModels
{
    public partial class TermsPageModel : LazySearchableCollectionPageModel<Term>
    {
        private TermService termsService => databaseManager.TermsService;

        public TermsPageModel(DatabaseManager databaseManager, PopupService popupService) : base(databaseManager, popupService, 25)
        {
            EditTermCommand = new Command(EditTerm);
            AddTermCommand = new Command(AddTermCommandExecute);
            AddClassCommand = new Command(AddClassCommandExecute);
            RemoveClassCommand = new Command(RemoveClassCommandExecute, RemoveClassCommandCanExecute);
            RemoveTermCommand = new Command(RemoveTerm, RemoveTermCommandCanExecute);
        }

        private async void RemoveTerm()
        {
            if (!await popupService.DisplayAlert("Confirm", $"Are you sure you want to remove {Selection}?", "Yes", "No"))
                return;

            try
            {
                await termsService.RemoveTerm(cancellationToken.Token, Selection);

                DisplayedCollection.Remove(Selection);
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }
        }

        private async void RemoveClassCommandExecute()
        {
            if (!await popupService.DisplayAlert("Confirm", $"Are you sure you want to remove {SelectedClass.ClassSchedule} from the term?", "Yes", "No"))
                return;

            try
            {
                await databaseManager.TermsService.RemoveClassAsync(cancellationToken.Token, Selection, SelectedClass.ClassSchedule);

                Selection.Classes.Remove(SelectedClass);
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }
        }

        private bool RemoveClassCommandCanExecute()
        {
            return SelectedClass is not null && SelectedClass?.Uses == 0;
        }
        private bool RemoveTermCommandCanExecute()
        {
            return Selection is not null && Selection?.Classes.Count == 0;
        }

        private async void AddClassCommandExecute()
        {
            await NavigationService.NavigateTo(nameof(SelectClassPageModel), Selection);
        }

        private async void EditTerm()
        {
            await NavigationService.NavigateToEditTermPage(Selection);
        }

        private TermClassSchedule selectedClass;
        public TermClassSchedule SelectedClass
        {
            get => selectedClass;
            set
            {
                if (value == selectedClass)
                    return;
                selectedClass = value;
                OnPropertyChanged();
                RemoveClassCommand?.ChangeCanExecute();
                RemoveTermCommand?.ChangeCanExecute();
            }
        }

        public Command AddClassCommand { get; init; }
        public Command EditTermCommand { get; init; }

        public Command RemoveClassCommand { get; init; }

        public Command UpdateTermsCommand { get; init; }
        public Command RemoveTermCommand { get; init; }

        private bool retrieveCompletedTerms = false;
        public bool RetrieveCompletedTerms
        {
            get => retrieveCompletedTerms;
            set
            {
                var changed = SetProperty(ref retrieveCompletedTerms, value);

                if (changed)
                {
                    RetrieveCollection();
                }
            }
        }

        public Command UpdateCommand { get; set; }
        public Command AddTermCommand { get; set; }

        private void TermAdded(int id)
        {
            var classSchedule = retrievedCollection.FirstOrDefault(item => item.Id == id);
            OnScrollToItem(classSchedule, false);
            Selection = classSchedule;
        }

        private async void AddTermCommandExecute()
        {
            await NavigationService.NavigateTo(nameof(AddTermPageModel), new Action<int>(TermAdded));
        }

        protected override List<Term> SortCollection(List<Term> collection)
        {
            return collection.OrderBy(t => t.StartDate).ToList();
        }

        protected async override Task<List<int>> GetDeletedIds()
        {
            return await termsService.GetDeletedTermsAsync(cancellationToken.Token, timeLastUpdated);
        }

        protected override Task<bool> HaveItemsFieldsChanged(CancellationToken cancellationToken, Term item, long timeLastUpdated)
        {
            return termsService.HasTermChanged(cancellationToken, item, timeLastUpdated);
        }

        protected override Task PopulateItemsFields(CancellationToken cancellationToken, Term item)
        {
            return termsService.PopulateTermsFields(cancellationToken, item);
        }

        protected override Task<List<Term>> RetrieveSearchedCollection(string query)
        {
            return termsService.GetTermsAsync(cancellationToken.Token, retrieveCompletedTerms);
        }

        protected override Task<List<Term>> RetrieveUnsearchedCollection()
        {
            return termsService.GetTermsAsync(cancellationToken.Token, retrieveCompletedTerms);
        }

        protected override Task<List<Term>> GetSearchedUpdatedItems(string query)
        {
            return termsService.GetUpdatedTermsAsync(cancellationToken.Token, timeLastUpdated, retrieveCompletedTerms);
        }

        protected override Task<List<Term>> GetUnsearchedUpdatedItems()
        {
            return termsService.GetUpdatedTermsAsync(cancellationToken.Token, timeLastUpdated, retrieveCompletedTerms);
        }

        protected override async void ChangeSelectedItem(Term item)
        {
            await base.ChangeSelectedItemAsync(item);
            RemoveTermCommand?.ChangeCanExecute();
        }
    }
}
