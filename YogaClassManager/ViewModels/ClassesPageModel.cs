using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YogaClassManager.Database;
using YogaClassManager.Models.Classes;
using YogaClassManager.Services;
using YogaClassManager.ViewModels.Base;

namespace YogaClassManager.ViewModels
{
    public partial class ClassesPageModel : CollectionPageModel<ClassSchedule>
    {
        public ClassesPageModel(DatabaseManager databaseManager, PopupService popupService) : base(databaseManager, popupService, 25)
        {
            AddClassCommand = new(addClassCommandExecute);
            EditClassScheduleCommand = new(EditClassSchedule);
            RemoveClassScheduleCommand = new(RemoveClassSchedule, CanRemoveClassSchedule);
            AddClassRollCommand = new(AddClassRollCommandExecute);
            UpdateClassRollCommand = new(UpdateClassRollCommandExecute, EditAndRemoveClassesCommandCanExecute);
            RemoveClassRollCommand = new(RemoveClassRollCommandExecute, EditAndRemoveClassesCommandCanExecute);
        }
        private async void EditClassSchedule()
        {
            await NavigationService.NavigateToEditClassSchedulePage(ClassSchedule.Copy(Selection));
        }

        private bool CanRemoveClassSchedule()
        {
            return ClassRolls.Count == 0 && Selection is not null;
        }

        private async void RemoveClassSchedule()
        {
            if (await popupService.DisplayAlert("Confirm", $"Are you sure you would like to delete {Selection}?\nThis cannot be undone.", "Yes", "Cancel"))
            {
                try
                {
                    await classesService.RemoveClassSchedule(CancellationToken.None, Selection);
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

                UpdateCollection();
            }
        }

        public ClassesService classesService => databaseManager.ClassesService;

        private async void UpdateClassRollCommandExecute()
        {
            if (SelectedClassRoll is null)
            {
                return;
            }

            await NavigationService.NavigateTo(nameof(MarkRollPageModel), Selection, null, SelectedClassRoll);
        }

        private async void AddClassRollCommandExecute()
        {
            await NavigationService.NavigateTo(nameof(MarkRollPageModel), Selection);
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateClassRollCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveClassRollCommand))]
        private ClassRoll selectedClassRoll;

        private bool retrieveArchivedClasses;

        public bool RetrieveArchivedClasses
        {
            get => retrieveArchivedClasses; set
            {
                var changed = SetProperty(ref retrieveArchivedClasses, value);

                if (changed)
                {
                    RetrieveCollection();
                }
            }
        }

        protected override void ChangeSelectedItem(ClassSchedule item)
        {
            base.ChangeSelectedItem(item);
            if (item is not null)
                RetrieveClassRolls();
            UpdateClassRollCommand?.NotifyCanExecuteChanged();
            RemoveClassRollCommand?.NotifyCanExecuteChanged();
            RemoveClassScheduleCommand?.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private ObservableCollection<ClassRoll> classRolls = new();

        private async void RetrieveClassRolls()
        {
            List<ClassRoll> classRolls;

            try
            {
                classRolls = await databaseManager.ClassesService.GetClassRollsFromClassAsync(CancellationToken.None, Selection);
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

            ClassRolls.Clear();
            AddRange(ClassRolls, classRolls);
            RemoveClassScheduleCommand?.NotifyCanExecuteChanged();
        }

        public RelayCommand UpdateCommand { get; set; }

        public RelayCommand AddClassCommand { get; set; }
        public RelayCommand EditClassScheduleCommand { get; set; }
        public RelayCommand AddClassRollCommand { get; set; }
        public RelayCommand UpdateClassRollCommand { get; private set; }
        public RelayCommand RemoveClassRollCommand { get; private set; }
        public RelayCommand RemoveClassScheduleCommand { get; private set; }

        private bool EditAndRemoveClassesCommandCanExecute()
        {
            return SelectedClassRoll is not null;
        }

        private void classAdded(int id)
        {
            var classSchedule = retrievedCollection.FirstOrDefault(student => student.Id == id);
            OnScrollToItem(classSchedule, false);
            Selection = classSchedule;
        }

        private async void addClassCommandExecute()
        {
            await NavigationService.NavigateTo(nameof(AddClassPageModel), new Action<int>(classAdded));
        }

        private async void RemoveClassRollCommandExecute()
        {
            if (selectedClassRoll is null)
            {
                return;
            }

            if (await popupService.DisplayAlert("Confirm action", "Are you sure you want to delete this class. This cannot be undone!", "Yes", "Cancel"))
            {
                try
                {
                    await databaseManager.ClassesService.RemoveClassRollAsync(CancellationToken.None, selectedClassRoll);
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

                ClassRolls.Remove(selectedClassRoll);
            }
        }

        protected override void UpdateCollection()
        {
            base.UpdateCollection();

            // TODO check if class rolls needs updating
            if (true && Selection is not null)
            {
                RetrieveClassRolls();
            }
        }

        protected override List<ClassSchedule> SortCollection(List<ClassSchedule> collection)
        {
            return collection.OrderBy(c => c.Id).ToList();
        }

        protected override Task<List<ClassSchedule>> GetCollection()
        {
            return classesService.GetClassesAsync(cancellationToken.Token, returnArchivedClassSchedules: RetrieveArchivedClasses);
        }

        protected override Task<List<ClassSchedule>> GetUpdatedItems()
        {
            return classesService.GetUpdatedClassSchedule(cancellationToken.Token, timeLastUpdated, RetrieveArchivedClasses);
        }

        protected override Task<List<int>> GetDeletedIds()
        {
            return classesService.GetDeletedClassScheduleIds(cancellationToken.Token, timeLastUpdated);
        }
    }
}
