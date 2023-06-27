using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels.Base
{
    public abstract class LazySearchableCollectionPageModel<T> : SearchableCollectionPageModel<T> where T : class, IIdentifiable, IUpdateable<T>
    {
        private Dictionary<int, long> timeItemLastUpdated = new();
        protected LazySearchableCollectionPageModel(DatabaseManager databaseManager, PopupService popupService, int forwardLoadingCount) : base(databaseManager, popupService, forwardLoadingCount)
        {
        }

        protected virtual async Task ChangeSelectedItemAsync(T item)
        {
            await TryUpdateItem(item);
            base.ChangeSelectedItem(item);
        }

        protected override async void ChangeSelectedItem(T item)
        {
            await TryUpdateItem(item);
            base.ChangeSelectedItem(item);
        }

        protected async override void UpdateCollection()
        {
            base.UpdateCollection();
            await TryUpdateItem(Selection);
        }

        protected virtual async Task<bool> TryUpdateItem(T item)
        {
            if (item is null)
                return false;

            StartBusy();

            try
            {
                if (!timeItemLastUpdated.ContainsKey(item.Id) ||
                    await HaveItemsFieldsChanged(cancellationToken.Token, item, timeItemLastUpdated.GetValueOrDefault(item.Id, 0)))
                {

                    await PopulateItemsFields(cancellationToken.Token, item);
                }
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
            finally
            {
                EndBusy();
            }

            timeItemLastUpdated.Remove(item.Id);
            timeItemLastUpdated.Add(item.Id, GetCurrentTimestamp());

            return true;
        }

        protected abstract Task<bool> HaveItemsFieldsChanged(CancellationToken cancellationToken, T item, long timeLastUpdated);

        protected abstract Task PopulateItemsFields(CancellationToken cancellationToken, T item);

        protected override Task SearchCollection()
        {
            timeItemLastUpdated.Clear();
            return base.SearchCollection();
        }

        protected override Task<List<T>> GetCollection()
        {
            timeItemLastUpdated.Clear();
            return base.GetCollection();
        }
    }
}
