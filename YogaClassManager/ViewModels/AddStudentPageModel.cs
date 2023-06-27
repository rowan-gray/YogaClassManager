using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.People;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(IdCallbackParameter), "idReturn")]
    [QueryProperty(nameof(StudentReturnParameter), "studentReturn")]
    public partial class AddStudentPageModel : ObservableObject
    {
        public Message IdCallbackParameter { set => IdCallback = (Action<int>)value.Parameter; }

        public Action<int> IdCallback { get; set; }
        public Message StudentReturnParameter { set => StudentReturn = (Action<Student>)value.Parameter; }

        public Action<Student> StudentReturn { get; set; }

        public Command CancelCommand { get; set; }
        public Command AddCommand { get; set; }
        public Command UpdateSaveCommandCanExecute { get; init; }
        public Command EditPassCommand { get; init; }
        public Command AddPassCommand { get; init; }
        public Command RemovePassCommand { get; init; }
        public Command EditEmergencyContactCommand { get; init; }
        public Command AddEmergencyContactCommand { get; init; }
        public Command RemoveEmergencyContactCommand { get; init; }
        public Command AddHealthConcernCommand { get; init; }
        public Command EditHealthConcernCommand { get; init; }
        public Command RemoveHealthConcernCommand { get; init; }

        private CancellationTokenSource cancellationToken = new();

        [ObservableProperty]
        private Student student;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        private Pass selectedPass;
        public Pass SelectedPass
        {
            get => selectedPass;
            set
            {
                if (SetProperty(ref selectedPass, value))
                    UpdatePassCommandsCanExecutes();
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

        public AddStudentPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;

            Student = new Student(-1, "", "", null, null, new(), new(), new(), true);

            CancelCommand = new(CancelCommandExecute);
            AddCommand = new(AddCommandExecute, AddCommandCanExecute);
            UpdateSaveCommandCanExecute = new(() => AddCommand?.ChangeCanExecute());
            EditPassCommand = new(EditPassCommandExecute, PassCommandsCanExecute);
            AddPassCommand = new(AddPassCommandExecute);
            RemovePassCommand = new(RemovePassCommandExecute, PassCommandsCanExecute);
            EditEmergencyContactCommand = new(EditEmergencyContactCommandExecute, EditEmergencyContactCommandCanExecute);
            AddEmergencyContactCommand = new(AddEmergencyContactCommandExecute);
            RemoveEmergencyContactCommand = new(RemoveEmergencyContactCommandExecute, EditEmergencyContactCommandCanExecute);
            AddHealthConcernCommand = new(AddHealthConcernCommandExecute);
            EditHealthConcernCommand = new(EditHealthConcernCommandExecute, ModifyHealthConcernCommandsCanExecute);
            RemoveHealthConcernCommand = new(RemoveHealthConcernCommandExecute, ModifyHealthConcernCommandsCanExecute);
        }

        private bool AddCommandCanExecute()
        {
            return Student.Validate();
        }

        private async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }

        private async void AddCommandExecute()
        {
            if (!Student.Validate())
            {
                await popupService.DisplayAlert("Invalid field/s", "One or more of the fields is not valid!", "Ok");
                return;
            }

            try
            {
                var id = await databaseManager.StudentsService.AddStudentAsync(cancellationToken.Token, Student);

                await NavigationService.GoBackAsync();
                IdCallback?.Invoke(id);
                Student.Id = id;
                StudentReturn?.Invoke(Student);
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                cancellationToken = new();
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }
        }

        private async void AddEmergencyContactCommandExecute()
        {
            await NavigationService.NavigateToAddEmergencyContactPage(Student, emergencyContactCallback: new(EmergencyContactAdded), saveToDatabase: false);
        }

        private void EmergencyContactAdded(EmergencyContact emergencyContact)
        {
            Student.ObservableEmergencyContacts.Add(emergencyContact);
        }

        private async void EditEmergencyContactCommandExecute()
        {
            if (SelectedEmergencyContact is null)
                return;

            await NavigationService.NavigateToEditEmergencyContactPage(SelectedEmergencyContact, emergencyContactCallback: new(EmergencyContactEdited), saveToDatabase: false);
        }

        private void EmergencyContactEdited(EmergencyContact emergencyContact)
        {
            var index = Student.ObservableEmergencyContacts.IndexOf(SelectedEmergencyContact);

            Student.ObservableEmergencyContacts.Remove(SelectedEmergencyContact);
            Student.ObservableEmergencyContacts.Insert(index, emergencyContact);
        }

        private async void RemoveEmergencyContactCommandExecute()
        {
            var _selectedEmergencyContact = SelectedEmergencyContact;
            // delay to avoid isEnabled visual glitch on remove button
            await Task.Delay(50);
            Student.ObservableEmergencyContacts.Remove(_selectedEmergencyContact);
        }

        private async void AddPassCommandExecute()
        {
            await NavigationService.NavigateToAddPassPage(Student.Copy(Student), passReturn: new(PassAdded), saveToDatabase: false);
        }

        private void PassAdded(Pass pass)
        {
            Student.ObservablePasses.Add(pass);
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

            await NavigationService.NavigateToEditPassPage(pass, passCallback: new(PassEdited), saveToDatabase: false);
        }

        private void PassEdited(Pass pass)
        {
            var index = Student.ObservablePasses.IndexOf(SelectedPass);

            Student.ObservablePasses.Remove(SelectedPass);
            Student.ObservablePasses.Insert(index, pass);
        }

        private async void RemovePassCommandExecute()
        {
            var _selectedPass = SelectedPass;
            // delay to avoid isEnabled visual glitch on remove button
            await Task.Delay(50);
            Student.ObservablePasses.Remove(_selectedPass);
        }
        private async void AddHealthConcernCommandExecute()
        {
            var healthConcerns = await popupService.DisplayPromptAsync("Health Concern", "What health concern would you like to add?");

            if (healthConcerns is null)
                return;

            foreach (var value in healthConcerns.Split(", "))
            {
                var healthConcern = value.Trim();
                healthConcern = healthConcern.Substring(0, 1).ToUpperInvariant() + healthConcern.Substring(1);
                Student.ObservableHealthConcerns.Add(healthConcern);
            }
        }
        private async void EditHealthConcernCommandExecute()
        {
            if (SelectedHealthConcern is null)
                return;

            var updatedHealthConcern = await popupService.DisplayPromptAsync("Health Concern", "What would you like this health concern to be?", initialValue: SelectedHealthConcern);

            // returns if cancelled
            if (updatedHealthConcern is null)
                return;

            updatedHealthConcern = updatedHealthConcern.Trim();
            updatedHealthConcern = updatedHealthConcern.Substring(0, 1).ToUpperInvariant() + updatedHealthConcern.Substring(1);

            var index = Student.ObservableHealthConcerns.IndexOf(SelectedHealthConcern);

            Student.ObservableHealthConcerns.Remove(SelectedHealthConcern);
            Student.ObservableHealthConcerns.Insert(index, updatedHealthConcern);
        }

        private async void RemoveHealthConcernCommandExecute()
        {
            if (SelectedHealthConcern is null)
                return;

            var _selectedHealthConcern = SelectedHealthConcern;
            // delay to avoid isEnabled visual glitch on remove button
            await Task.Delay(50);
            Student.ObservableHealthConcerns.Remove(_selectedHealthConcern);
        }

        private bool PassCommandsCanExecute()
        {
            return SelectedPass is not null;
        }
        private bool EditEmergencyContactCommandCanExecute()
        {
            return SelectedEmergencyContact is not null;
        }
        private bool ModifyHealthConcernCommandsCanExecute()
        {
            return SelectedHealthConcern is not null;
        }

        private void UpdateHealthConcernsCommandsCanExecute()
        {
            AddHealthConcernCommand?.ChangeCanExecute();
            EditHealthConcernCommand?.ChangeCanExecute();
            RemoveHealthConcernCommand?.ChangeCanExecute();
        }

        private void UpdateEmergencyContactsCommands()
        {
            AddEmergencyContactCommand?.ChangeCanExecute();
            EditEmergencyContactCommand?.ChangeCanExecute();
            RemoveEmergencyContactCommand?.ChangeCanExecute();
        }

        private void UpdatePassCommandsCanExecutes()
        {
            EditPassCommand?.ChangeCanExecute();
            RemovePassCommand?.ChangeCanExecute();
            AddPassCommand?.ChangeCanExecute();
        }
    }
}
