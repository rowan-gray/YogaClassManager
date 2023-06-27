using YogaClassManager.Database;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.People;
using YogaClassManager.Models.People.EventArguments;
using YogaClassManager.Services;
using YogaClassManager.ViewModels.Base;

namespace YogaClassManager.ViewModels
{
    public delegate void ScrollToIndexEventHandler(object source, ScrollToIndexEventArgs e);

    public partial class StudentsPageModel : LazySearchableCollectionPageModel<Student>
    {
        private StudentsService studentsService => databaseManager.StudentsService;

        private string selectedHealthConcern;
        public string SelectedHealthConcern
        {
            get => selectedHealthConcern;
            set
            {
                selectedHealthConcern = value;
                OnPropertyChanged();
                UpdateHealthConcernsCommandsCanExecute();
            }
        }

        private void UpdateHealthConcernsCommandsCanExecute()
        {
            AddHealthConcernCommand?.ChangeCanExecute();
            RemoveHealthConcernCommand?.ChangeCanExecute();
        }

        private bool showInactiveStudents = false;

        public bool ShowInactiveStudents
        {
            get => showInactiveStudents;
            set
            {
                showInactiveStudents = value;
                RetrieveCollection();
                OnPropertyChanged();
            }
        }
        private Pass selectedPass;
        public Pass SelectedPass
        {
            get => selectedPass;
            set
            {
                selectedPass = value;
                OnPropertyChanged();
                UpdatePassCanExecutesCommand();
            }
        }
        private EmergencyContact selectedEmergencyContact;
        public EmergencyContact SelectedEmergencyContact
        {
            get => selectedEmergencyContact;
            set
            {
                selectedEmergencyContact = value;
                OnPropertyChanged();
                UpdateEmergencyContactsCommands();
            }
        }

        public Command EditDetailsCommand { get; init; }
        public Command EditPassCommand { get; init; }
        public Command AddPassCommand { get; init; }
        public Command RemovePassCommand { get; init; }
        public Command UpdatePassCanExecute { get; init; }
        public Command EditEmergencyContactCommand { get; init; }
        public Command AddEmergencyContactCommand { get; init; }
        public Command RemoveEmergencyContactCommand { get; init; }
        public Command AddStudentCommand { get; init; }
        public Command RemoveStudentCommand { get; init; }
        public Command UpdateStudentsCommand { get; init; }
        public Command AddHealthConcernCommand { get; init; }
        public Command RemoveHealthConcernCommand { get; init; }
        public Command AdvancedPassesViewCommand { get; init; }

        public StudentsPageModel(DatabaseManager databaseManager, PopupService popupService) : base(databaseManager, popupService, 50)
        {
            EditDetailsCommand = new(EditDetailsCommandExecute);
            EditPassCommand = new(EditPassCommandExecute, PassCommandsCanExecute);
            AddPassCommand = new(AddPassCommandExecute);
            RemovePassCommand = new(RemovePassCommandExecute, PassCommandsCanExecute);
            UpdatePassCanExecute = new(UpdatePassCanExecutesCommand);
            EditEmergencyContactCommand = new(EditEmergencyContactCommandExecute, EditEmergencyContactCommandCanExecute);
            AddEmergencyContactCommand = new(AddEmergencyContactCommandExecute);
            RemoveEmergencyContactCommand = new(RemoveEmergencyContactCommandExecute, EditEmergencyContactCommandCanExecute);
            AddStudentCommand = new(AddStudentCommandExecute);
            RemoveStudentCommand = new(RemoveStudentCommandExecute, RemoveStudentCommandCanExecute);
            AddHealthConcernCommand = new(AddHealthConcernCommandExecute);
            RemoveHealthConcernCommand = new(RemoveHealthConcernCommandExecute, RemoveHealthConcernCommandCanExecute);
            AdvancedPassesViewCommand = new(AdvancedPassesViewExecute);
        }

        private void AdvancedPassesViewExecute()
        {
            NavigationService.NavigateToStudentPassesPage(Selection);
        }


        protected override void ChangeSelectedItem(Student item)
        {
            base.ChangeSelectedItem(item);
            RemoveStudentCommand?.ChangeCanExecute();
        }

        protected override async Task<bool> TryUpdateItem(Student item)
        {
            var selectedPassId = SelectedPass?.Id;
            var selectedEmergencyContactId = SelectedEmergencyContact?.Id;
            var _selectedHealthConcern = SelectedHealthConcern;

            var updated = await base.TryUpdateItem(item);

            if (updated)
            {
                SelectedPass = item?.Passes.FirstOrDefault(pass => pass.Id == selectedPassId);
                SelectedEmergencyContact = item?.EmergencyContacts.FirstOrDefault(emergencyContact => emergencyContact.Id == selectedEmergencyContactId);
                SelectedHealthConcern = item?.HealthConcerns.FirstOrDefault(healthConcern => healthConcern == _selectedHealthConcern);
            }

            return updated;
        }

        private void CancelTasksExecute()
        {
            cancellationToken.Cancel();
        }

        private void ResetPageCommandExecute()
        {
            cancellationToken = new();
            UpdateCollection();
        }

        private async void RemovePassCommandExecute()
        {
            if (Selection is null || SelectedPass is null)
                return;

            if (!await popupService.DisplayAlert("Confirm", $"Are you sure you want to remove the following pass?\n{SelectedPass}", "Yes", "No"))
                return;

            StartBusy();
            try
            {
                if (SelectedPass.ClassesUsed == 0)
                {
                    try
                    {
                        await databaseManager.PassesService.RemovePassAsync(cancellationToken.Token, SelectedPass);

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
                else
                {
                    if (!await popupService.DisplayAlert("Confirm", "Some classes have already been redeemed off this pass.\n" +
                        "Would you like the pass to be altered to remove all remaining passes.", "Yes", "No"))
                        return;
                    try
                    {
                        await databaseManager.PassesService.AddPassAlterationAsync(cancellationToken.Token, new PassAlteration(-1, SelectedPass.Id, -SelectedPass.ClassesRemaining, "AUTOMATIC OPERATION: Pass Removed"));

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
            }
            finally
            {
                EndBusy();
            }

            await TryUpdateItem(Selection);
        }

        private async void RemoveEmergencyContactCommandExecute()
        {
            if (Selection is null || SelectedEmergencyContact is null)
                return;

            if (!await popupService.DisplayAlert("Confirm", $"Are you sure you want to remove the following emergency contact? \n{SelectedEmergencyContact.FullName}", "Yes", "No"))
                return;

            StartBusy();
            try
            {
                await databaseManager.StudentsService.RemoveEmergencyContactAsync(cancellationToken.Token, SelectedEmergencyContact);
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }

            EndBusy();
            await TryUpdateItem(Selection);
        }

        private bool RemoveHealthConcernCommandCanExecute()
        {
            return SelectedHealthConcern is not null;
        }

        private async void RemoveHealthConcernCommandExecute()
        {
            if (Selection is null || SelectedHealthConcern is null)
                return;

            if (!await popupService.DisplayAlert("Confirm", $"Are you sure you want to remove the following health concern? \n{SelectedHealthConcern}", "Yes", "No"))
                return;

            StartBusy();
            try
            {
                await databaseManager.StudentsService.RemoveHealthConcern(cancellationToken.Token, Selection, SelectedHealthConcern);
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }
            EndBusy();

            await TryUpdateItem(Selection);
        }

        private async void AddHealthConcernCommandExecute()
        {
            if (Selection is null)
                return;

            var healthConcerns = await popupService.DisplayPromptAsync("Health Concern", "What health concern would you like to add?");

            if (healthConcerns is null)
                return;

            StartBusy();
            try
            {
                foreach (var value in healthConcerns.Split(", "))
                {
                    var healthConcern = value.Trim();
                    // TODO change this to run in a transaction
                    await databaseManager.StudentsService.AddHealthConcern(cancellationToken.Token, Selection, healthConcern.Substring(0, 1).ToUpperInvariant() + healthConcern.Substring(1));
                }
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }
            EndBusy();

            await TryUpdateItem(Selection);
        }

        private void UpdateEmergencyContactsCommands()
        {
            AddEmergencyContactCommand?.ChangeCanExecute();
            EditEmergencyContactCommand?.ChangeCanExecute();
            RemoveEmergencyContactCommand?.ChangeCanExecute();
        }

        private bool EditEmergencyContactCommandCanExecute()
        {
            return SelectedEmergencyContact is not null;
        }

        private async void RemoveStudentCommandExecute()
        {
            if (Selection is null)
                return;

            StartBusy();
            if (Selection.IsActive)
            {
                try
                {
                    await databaseManager.PeopleService.HidePersonAsync(cancellationToken.Token, Selection);
                    if (!ShowInactiveStudents)
                    {
                        DisplayedCollection.Remove(Selection);
                        Selection = DisplayedCollection.FirstOrDefault();
                    }
                    else
                    {
                        Selection.IsActive = false;
                    }
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
            else
            {
                try
                {
                    await databaseManager.PeopleService.UnhidePersonAsync(cancellationToken.Token, Selection);
                    Selection.IsActive = true;
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
            EndBusy();
        }

        private bool PassCommandsCanExecute()
        {
            return SelectedPass is not null;
        }
        private bool RemoveStudentCommandCanExecute()
        {
            return Selection is not null;
        }

        private void UpdatePassCanExecutesCommand()
        {
            EditPassCommand?.ChangeCanExecute();
            RemovePassCommand?.ChangeCanExecute();
            AddPassCommand?.ChangeCanExecute();
        }

        private async void AddStudentCommandExecute()
        {
            await NavigationService.NavigateToAddStudentPage(studentReturn: new(StudentAdded));
        }

        private async void EditEmergencyContactCommandExecute()
        {
            if (SelectedEmergencyContact is null)
                return;

            await NavigationService.NavigateToEditEmergencyContactPage(EmergencyContact.Copy(SelectedEmergencyContact));
        }

        private async void AddEmergencyContactCommandExecute()
        {
            await NavigationService.NavigateToAddEmergencyContactPage(Student.Copy(Selection));
        }


        private async void EditDetailsCommandExecute()
        {
            //await NavigationService.NavigateTo(nameof(EditDetailsPage), Student.Copy(Selection));
        }
        private async void AddPassCommandExecute()
        {
            await NavigationService.NavigateToAddPassPage(Student.Copy(Selection));
        }

        private async void EditPassCommandExecute()
        {
            if (SelectedPass is null)
                return;

            Pass pass;

            if (selectedPass.GetType() == typeof(DatedPass))
            {
                pass = DatedPass.Copy((DatedPass)selectedPass);
            }
            else if (selectedPass.GetType() == typeof(TermPass))
            {
                pass = TermPass.Copy((TermPass)selectedPass);
            }
            else
            {
                pass = Pass.Copy(selectedPass);
            }

            await NavigationService.NavigateToEditPassPage(pass);
        }

        private async void StudentAdded(Student addedStudent)
        {
            if (!retrievedCollection.Exists(student => student.Id == addedStudent.Id))
            {
                CurrentSearchQuery = addedStudent.FullName;
                await SearchCollection();
            }

            OnScrollToItem(addedStudent, false);
            Selection = retrievedCollection.FirstOrDefault(student => student.Id == addedStudent.Id);
        }

        protected override Task<List<Student>> RetrieveSearchedCollection(string query)
        {
            return databaseManager.StudentsService.SearchStudentsAsync(cancellationToken.Token, query, null, ShowInactiveStudents);
        }

        protected override Task<List<Student>> RetrieveUnsearchedCollection()
        {
            return studentsService.GetStudentsAsync(cancellationToken.Token, ShowInactiveStudents);
        }

        protected override Task<List<Student>> GetSearchedUpdatedItems(string query)
        {            
            return studentsService.GetUpdatedStudentsMatchingQuery(cancellationToken.Token, timeLastUpdated, CurrentSearchQuery, ShowInactiveStudents);
        }

        protected override Task<List<Student>> GetUnsearchedUpdatedItems()
        {           
            return studentsService.GetUpdatedStudents(cancellationToken.Token, timeLastUpdated, ShowInactiveStudents);
        }

        protected override List<Student> SortCollection(List<Student> collection)
        {
            return collection.OrderBy(student => student.FullName).ToList();
        }

        protected override Task<List<int>> GetDeletedIds()
        {
                return studentsService.GetDeletedStudentsIds(cancellationToken.Token, timeLastUpdated);
        }

        protected override Task<bool> HaveItemsFieldsChanged(CancellationToken cancellationToken, Student item, long timeLastUpdated)
        {
                return databaseManager.StudentsService.HaveFieldsChanged(cancellationToken, item, timeLastUpdated);
        }

        protected override Task PopulateItemsFields(CancellationToken cancellationToken, Student item)
        {
               return studentsService.PopulateStudentsFields(cancellationToken, item, true);
        }
    }
}
