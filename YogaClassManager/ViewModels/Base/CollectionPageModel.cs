using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.People.EventArguments;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels.Base
{
    public abstract partial class CollectionPageModel<T> : BasePageModel where T : class, IIdentifiable, IUpdateable<T>
    {
        protected long timeLastUpdated;
        protected CancellationTokenSource cancellationToken = new CancellationTokenSource();
        protected int forwardLoadingCount;
        protected readonly DatabaseManager databaseManager;
        protected readonly PopupService popupService;

        protected T selection;

        public T Selection
        {
            get => selection;
            set
            {
                ChangeSelectedItem(value);
            }
        }

        protected virtual void ChangeSelectedItem(T item)
        {
            SetProperty(ref selection, item, nameof(Selection));
        }

        [ObservableProperty]
        private ObservableCollection<T> displayedCollection;
        protected List<T> retrievedCollection;

        public event ScrollToIndexEventHandler ScrollToIndex;
        protected void OnScrollToIndex(int index, bool animate)
        {
            if (ScrollToIndex is not null)
            {
                ScrollToIndex(this, new ScrollToIndexEventArgs(index, animate));
            }
        }

        protected void OnScrollToItem(T item, bool animate)
        {
            if (item is null)
                return;

            if (!retrievedCollection.Exists(i => i.Id == item.Id))
            {
                return;
            }

            item = retrievedCollection.First(i => i.Id == item.Id);

            if (!DisplayedCollection.Contains(item))
            {
                AddRange(DisplayedCollection,
                    GetRangeOrLess(retrievedCollection,
                    DisplayedCollection.Count,
                    retrievedCollection.IndexOf(item) + 1
                    + forwardLoadingCount));
            }

            OnScrollToIndex(DisplayedCollection.IndexOf(item), animate);
        }

        public Command ResetPageCommand { get; init; }
        public Command EndOfListCommand { get; init; }
        public Command CancelTasks { get; init; }

        public CollectionPageModel(DatabaseManager databaseManager, PopupService popupService, int forwardLoadingCount, bool autoRetrieve = true)
        {
            ResetPageCommand = new(ResetPage);
            EndOfListCommand = new(EndOfList);
            CancelTasks = new(CancelTasksExecute);
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            this.forwardLoadingCount = forwardLoadingCount;
            DisplayedCollection = new();
            if (autoRetrieve)
                RetrieveCollection();
        }
        private void CancelTasksExecute()
        {
            cancellationToken.Cancel();
            cancellationToken.Dispose();
        }

        protected virtual async void UpdateCollection()
        {
            if (DisplayedCollection is null || retrievedCollection is null)
            {
                return;
            }

            StartBusy();
            List<T> updatedItems;
            List<int> deletedIds;
            try
            {
                updatedItems = await GetUpdatedItems();
                deletedIds = await GetDeletedIds();
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                EndBusy();
                return;
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                EndBusy();
                return;
            }


            if (deletedIds.Count > 0)
            {
                RemoveItemsUsingId(deletedIds);
            }

            if (updatedItems.Count > 0)
            {
                var newPeople = UpdatedRetrievedCollection(updatedItems);

                InsertNewPeopleIntoDisplay(newPeople);
            }

            timeLastUpdated = GetCurrentTimestamp(); 
            EndBusy();
        }

        protected async void RetrieveCollection()
        {
            await RetrieveCollectionAsync();
        }

        protected async Task RetrieveCollectionAsync()
        {
            StartBusy();
            try
            {
                try
                {
                    retrievedCollection = await GetCollection();
                }
                catch (TaskCanceledException)
                {
                    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                    throw;
                }
                catch (Exception e)
                {
                    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                    throw;
                }

                DisplayedCollection.Clear();
                retrievedCollection = SortCollection(retrievedCollection);
                AddRange(DisplayedCollection, GetRangeOrLess(retrievedCollection, 0, forwardLoadingCount));
                Selection = DisplayedCollection.FirstOrDefault();
                timeLastUpdated = GetCurrentTimestamp();
            }
            finally
            {
                EndBusy();
            }
        }

        private void ResetPage()
        {
            cancellationToken = new();
            UpdateCollection();
        }


        private void EndOfList()
        {
            var range = GetRangeOrLess(retrievedCollection, DisplayedCollection.Count, forwardLoadingCount);

            if (range.Count > 0)
            {
                AddRange(DisplayedCollection, range);
            }
        }

        protected List<T> GetRangeOrLess(List<T> list, int index, int count)
        {
            if (index + count > list.Count)
            {
                count = list.Count - index;
            }
            return list.GetRange(index, count);
        }

        protected long GetCurrentTimestamp()
        {
            var now = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1));
            return (long)now.TotalMilliseconds;
        }

        protected void AddRange<K>(ObservableCollection<K> observableCollection, IEnumerable<K> items)
        {
            foreach (var item in items)
                observableCollection.Add(item);
        }
        protected void RemoveItemsUsingId(List<int> deletedItemsId)
        {
            if (deletedItemsId.Count > 0)
            {
                foreach (var id in deletedItemsId)
                {
                    if (Selection?.Id == id)
                    {
                        Selection = default;
                    }

                    var index = retrievedCollection.FindIndex(item => item.Id == id);
                    if (index >= 0)
                        retrievedCollection.RemoveAt(index);
                    index = DisplayedCollection.ToList().FindIndex(item => item.Id == id);
                    if (index >= 0)
                        DisplayedCollection.RemoveAt(index);
                }
            }
        }

        private List<T> UpdatedRetrievedCollection(List<T> updatedItems)
        {
            List<T> newItems = new();

            foreach (var updatedItem in updatedItems)
            {
                var retrievedItem = retrievedCollection.Find(item => item.Id == updatedItem.Id);
                if (retrievedItem is not null)
                {
                    retrievedItem.Update(updatedItem);
                }
                else
                {
                    newItems.Add(updatedItem);
                }
            }

            return newItems;
        }

        private void InsertNewPeopleIntoDisplay(List<T> newItems)
        {
            retrievedCollection.AddRange(newItems);
            retrievedCollection = SortCollection(retrievedCollection);
            foreach (var newPerson in newItems)
            {
                var index = retrievedCollection.IndexOf(newPerson);
                if (index < DisplayedCollection.Count + newItems.Count)
                {
                    DisplayedCollection.Insert(index, newPerson);
                }
            }
        }

        protected abstract List<T> SortCollection(List<T> collection);
        protected abstract Task<List<T>> GetCollection();

        protected abstract Task<List<T>> GetUpdatedItems();

        protected abstract Task<List<int>> GetDeletedIds();
    }
}
