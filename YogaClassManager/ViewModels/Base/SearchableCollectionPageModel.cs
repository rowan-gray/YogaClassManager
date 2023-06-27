using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels.Base
{
    public abstract class SearchableCollectionPageModel<T> : CollectionPageModel<T> where T : class, IIdentifiable, IUpdateable<T>
    {

        private string editableSearchQuery = null;

        private string lastSearchQuery = null;

        protected SearchableCollectionPageModel(DatabaseManager databaseManager, PopupService popupService, int forwardLoadingCount, bool autoRetrieve = true) : base(databaseManager, popupService, forwardLoadingCount, autoRetrieve)
        {
            SearchCollectionCommand = new(SearchCollectionCommandExecute);
        }

        public Command SearchCollectionCommand { get; init; }
        public string CurrentSearchQuery
        {
            get => editableSearchQuery;
            set
            {
                if (value == editableSearchQuery)
                {
                    return;
                }

                OnPropertyChanging();

                if (value is null)
                {
                    editableSearchQuery = null;
                }
                else if (value.Trim().Length == 0)
                {
                    editableSearchQuery = null;
                }
                else
                {
                    editableSearchQuery = value.TrimStart();
                }

                OnPropertyChanged();
            }
        }

        private void SearchCollectionCommandExecute()
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            SearchCollection();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        protected virtual async Task SearchCollection()
        {
            if (CurrentSearchQuery == lastSearchQuery)
            return;

            StartBusy();
            try
            {

                try
                {
                    if (CurrentSearchQuery is null)
                    {
                        await RetrieveCollectionAsync();
                        lastSearchQuery = null;
                        return;
                    }
                    retrievedCollection = await RetrieveSearchedCollection(CurrentSearchQuery);
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

                DisplayedCollection.Clear();
                AddRange(DisplayedCollection, GetRangeOrLess(retrievedCollection, 0, 50));
                lastSearchQuery = CurrentSearchQuery;
                Selection = DisplayedCollection.FirstOrDefault();
            }
            finally
            {
                EndBusy();
            }
        }

        protected override Task<List<T>> GetCollection()
        {
            if (CurrentSearchQuery is null)
                return RetrieveUnsearchedCollection();
            else
                return RetrieveSearchedCollection(CurrentSearchQuery);
        }

        protected override Task<List<T>> GetUpdatedItems()
        {
                if (CurrentSearchQuery is null)
                    return GetUnsearchedUpdatedItems();
                else
                    return GetSearchedUpdatedItems(CurrentSearchQuery);
        }

        protected abstract Task<List<T>> RetrieveSearchedCollection(string query);
        protected abstract Task<List<T>> RetrieveUnsearchedCollection();
        protected abstract Task<List<T>> GetSearchedUpdatedItems(string query);
        protected abstract Task<List<T>> GetUnsearchedUpdatedItems();
    }
}
