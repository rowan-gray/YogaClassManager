using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Classes;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(CallbackParameter), "parameter2")]
    [QueryProperty(nameof(TermParameter), "parameter1")]
    public partial class SelectClassPageModel : ObservableObject
    {
        public Message CallbackParameter { set => Callback = (Action)value.Parameter; }
        public Message TermParameter { set { Term = (Term)value.Parameter; RetrieveClasses(); } }

        public Action Callback { get; set; }

        [ObservableProperty]
        private ObservableCollection<ClassSchedule> availableClasses;
        [ObservableProperty]
        private ObservableCollection<TermClassSchedule> selectedClasses;
        [ObservableProperty]
        private Term term;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public SelectClassPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            SelectedClasses = new();
            AddCommand = new Command(AddCommandExecute, () => selectedClasses.Count != 0);
            CancelCommand = new Command(CancelCommandExecute);
            SelectClassCommand = new(SelectClassCommandExecute);
            RemoveClassCommand = new(RemoveClassCommandExecute);
            ChangeClassCountCommand = new(ChangeClassCountCommandExecute);
        }

        public void SelectClassCommandExecute(object obj)
        {
            if (obj is null)
                return;

            if (obj.GetType() != typeof(ClassSchedule))
                return;

            var classSchedule = (ClassSchedule)obj;

            if (AvailableClasses.Remove(classSchedule))
            {
                var timeSpan = Term.EndDate.ToDateTime(TimeOnly.MinValue) - Term.StartDate.ToDateTime(TimeOnly.MinValue);
                var count = timeSpan.Days / 7;
                SelectedClasses.Add(new(classSchedule, count, 0));
            }

            AddCommand.ChangeCanExecute();
        }

        public void RemoveClassCommandExecute(object obj)
        {
            if (obj is null)
                return;

            if (obj.GetType() != typeof(TermClassSchedule))
                return;

            var termClassSchedule = (TermClassSchedule)obj;

            if (SelectedClasses.Remove(termClassSchedule))
                AvailableClasses.Add(termClassSchedule.ClassSchedule);

            AddCommand.ChangeCanExecute();
        }

        public async void ChangeClassCountCommandExecute(object obj)
        {
            if (obj is null)
                return;

            if (obj.GetType() != typeof(TermClassSchedule))
                return;

            var termClassSchedule = (TermClassSchedule)obj;

            var response = await popupService.DisplayPromptAsync("Change Class Count", $"Please enter the new class count for {termClassSchedule.ClassSchedule}.",
                initialValue: termClassSchedule.ClassCount.ToString(), placeholder: "Class Count");

            if (response is not null)
            {
                try
                {
                    var classCount = Convert.ToUInt16(response);
                    if (classCount == 0)
                    {
                        throw new ArgumentException($"Cannot have a class count of {classCount}");
                    }
                    termClassSchedule.ClassCount = (int)classCount;
                    var index = SelectedClasses.IndexOf(termClassSchedule);
                    SelectedClasses.Remove(termClassSchedule);
                    SelectedClasses.Insert(index, termClassSchedule);
                }
                catch (Exception ex) when (ex is FormatException ||
                               ex is OverflowException)
                {
                    await popupService.DisplayAlert("Incorrect format", "There was an error converting your input into a number.\nPlease try again.", "OK");
                }
                catch (ArgumentException e)
                {
                    await popupService.DisplayAlert("Invalid class count", e.Message, "OK");
                }
            }
        }

        public async void RetrieveClasses()
        {
            try
            {
                AvailableClasses = new(await databaseManager.ClassesService.GetClassesAsync(CancellationToken.None, Term.Classes.Select(termClass => termClass.ClassSchedule).ToList()));
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                AvailableClasses = default;
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                AvailableClasses = default;
            }
        }

        public async void AddCommandExecute()
        {
            try
            {
                await databaseManager.TermsService.SaveTermsClassesAsync(CancellationToken.None, Term.Id, SelectedClasses.ToList());
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

        public Command SelectClassCommand { get; init; }

        public Command AddCommand { get; init; }

        public Command CancelCommand { get; init; }
        public Command RemoveClassCommand { get; init; }
        public Command ChangeClassCountCommand { get; init; }
    }
}
