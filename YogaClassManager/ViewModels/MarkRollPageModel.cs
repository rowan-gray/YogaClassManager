#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Classes;
using YogaClassManager.Models.People;
using YogaClassManager.Models.Passes;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{

    public partial class MarkRollPageModel : ObservableObject, IQueryAttributable
    {
        [ObservableProperty]
        private ClassSchedule? classSchedule;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;
        private int classRollId = -1;
        private bool queryAttributesApplied = false;
        [ObservableProperty]
        private string? headerText;

        public delegate void RequestSearchBarFocusHandler(object source, EventArgs e);
        public event RequestSearchBarFocusHandler RequestSearchBarFocus;

        [ObservableProperty]
        private DateOnly date;
        [ObservableProperty]
        private string searchFeedback;

        [ObservableProperty]
        private ObservableCollection<Student> searchedStudents;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private ObservableCollection<ClassRollEntry> markedStudents;
        [ObservableProperty]
        private string? search;
        private Student? studentToAdd;
        private ClassRoll? initialClassRoll;

        public Action? Callback { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MarkRollPageModel(DatabaseManager databaseManager, PopupService popupService)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            SearchStudentsCommand = new(SearchStudentsCommandExecute);
            AddStudentCommand = new(AddStudentCommandExecute);
            RemoveStudentCommand = new(RemoveStudentCommandExecute);
            SaveCommand = new(SaveCommandExecute, SaveCommandCanExecute);
            CancelCommand = new(CancelCommandExecute);
            SaveCommandCanExecuteChangedCommand = new(SaveCommandCanExecuteChanged);
            UpdatePassCommand = new(UpdatePass);
            MarkedStudents = new();
            SearchedStudents = new();
            SearchFeedback = "Please search a student";
            Date = DateOnly.FromDateTime(DateTime.Now);
        }

        private void SaveCommandCanExecuteChanged()
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (!queryAttributesApplied)
            {
                if (query.ContainsKey("parameter1"))
                    ClassSchedule = (ClassSchedule)((Message)query["parameter1"]).Parameter;
                if (query.ContainsKey("parameter2"))
                    Callback = (Action)((Message)query["parameter2"]).Parameter;
                if (query.ContainsKey("parameter3"))
                {
                    var classRoll = (ClassRoll)((Message)query["parameter3"]).Parameter;
                    initialClassRoll = classRoll;
                    classRollId = classRoll.Id;
                    MarkedStudents = new ObservableCollection<ClassRollEntry>(classRoll.StudentEntries);
                    foreach (var entry in classRoll.StudentEntries)
                    {
                        entry.Pass.ClassesUsed -= 1;
                    }
                    Date = classRoll.Date;
                    HeaderText = $"Editing {classRoll}";
                }
                else
                {
                    HeaderText = $"New Class ({ClassSchedule})";

                    //try
                    //{
                        classRollId = await databaseManager.ClassesService.AddClassRollAsync(CancellationToken.None, new(-1, Date, ClassSchedule, new()));
                    //}
                    //catch (TaskCanceledException)
                    //{
                    //    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                    //    return;
                    //}
                    //catch (Exception e)
                    //{
                    //    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                    //    return;
                    //}
                }

                queryAttributesApplied = true;
            }
        }

        private bool SaveCommandCanExecute()
        {
            return MarkedStudents.Count > 0;
        }

        private async void SaveCommandExecute()
        {
            var classRoll = new ClassRoll(classRollId, Date, ClassSchedule, MarkedStudents.ToList());

            if (Date.DayOfWeek != ClassSchedule?.Day)
            {
                if (!await popupService.DisplayAlert("Confirm", $"The selected date does not fall on a {ClassSchedule?.Day}.\nAre you sure you would like save?", "Yes", "No"))
                {
                    return;
                }
            }

            //try
            //{
                await databaseManager.ClassesService.UpdateClassRollDetailsAsync(CancellationToken.None, new(classRollId, Date, ClassSchedule, MarkedStudents.ToList()));
            //}
            //catch (TaskCanceledException)
            //{
            //    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            //    return;
            //}
            //catch (Exception e)
            //{
            //    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            //    return;
            //}

            await NavigationService.GoBackAsync();
            Callback?.Invoke();
        }

        private async void CancelCommandExecute()
        {
            if (await popupService.DisplayAlert("Confirm", "Are you sure you want to cancel this sign in?", "Yes", "No"))
            {
                if (initialClassRoll is null)
                {
                    await databaseManager.ClassesService.RemoveClassRollAsync(CancellationToken.None, new(classRollId, Date, ClassSchedule, MarkedStudents.ToList()));
                }
                else
                {
                    await databaseManager.ClassesService.UpdateClassRollAsync(CancellationToken.None, initialClassRoll);
                }

                await NavigationService.GoBackAsync();
            }
        }

        private async void RemoveStudentCommandExecute(object obj)
        {
            if (obj.GetType() != typeof(ClassRollEntry))
                return;

            var classRollEntry = (ClassRollEntry)obj;

            //try
            //{
                await databaseManager.ClassesService.RemoveStudentFromClassRollAsync(CancellationToken.None, classRollId, classRollEntry);
            //}
            //catch (TaskCanceledException)
            //{
            //    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            //    return;
            //}
            //catch (Exception e)
            //{
            //    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            //    return;
            //}

            MarkedStudents.Remove(classRollEntry);
        }

        private async void UpdatePass(object obj)
        {
            if (obj is null) {
                return;
            }

            if (obj.GetType() != typeof(ClassRollEntry))
                return;

            var classRollEntry = (ClassRollEntry)obj;

            //try
            //{
                await databaseManager.ClassesService.UpdateClassRollEntryAsync(CancellationToken.None, classRollId, classRollEntry);
            //}
            //catch (TaskCanceledException)
            //{
            //    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            //    return;
            //}
            //catch (Exception e)
            //{
            //    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            //    return;
            //}
        }
        private async void AddStudentCommandExecute(object obj)
        {
            if (obj.GetType() != typeof(Student))
                return;

            await AddStudentToRoll((Student)obj);
        }

        private async Task AddStudentToRoll(Student student)
        {
            studentToAdd = student;

            if (studentToAdd.Passes is null)
            {
                //try
                //{
                    await databaseManager.StudentsService.PopulateStudentsFields(CancellationToken.None, studentToAdd, true);
                //}
                //catch (TaskCanceledException)
                //{
                //    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                //    return;
                //}
                //catch (Exception e)
                //{
                //    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                //    return;
                //}
            }

            if (studentToAdd?.Passes?.Count == 0)
            {
                if (await popupService.DisplayAlert("Insufficient Passes",
                    $"{studentToAdd.FullName} does not have any passes remaining.\nWould you like to add a pass?",
                    "Yes", "No"))
                {
                    await NavigationService.NavigateToAddPassPage(Student.Copy(studentToAdd), new Action(StudentPassAdded), null, classSchedule);
                }
                return;
            }

            Pass? currentSelectedPass = null;
            int? currentPriority = null;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            foreach (var pass in studentToAdd?.Passes)
            {
                var priority = pass.GetPassUsagePriority(ClassSchedule, Date);

                if (priority is null)
                {
                    continue;
                }

                if (currentPriority is null || priority < currentPriority)
                {
                    currentPriority = priority;
                    currentSelectedPass = pass;
                }

                if (currentPriority == 1)
                {
                    break;
                }
            }

            if (currentSelectedPass is null)
            {
                List<string> options = new(){ "Add new pass" };

                foreach (var pass in studentToAdd?.Passes)
                {
                    if (!pass.IsExpired && !options.Contains("Use another pass"))
                    {
                        options.Add("Use another pass");
                    }
                    if (pass.IsExpired && !options.Contains("Use expired pass"))
                    {
                        options.Add("Use expired pass");
                    }

                    if (options.Contains("Use another pass") && options.Contains("Use expired pass"))
                    {
                        break;
                    }
                }

                var response = await popupService.DisplayActionSheet($"{studentToAdd.FullName} has no valid passes.\nWhat would you like to do?",
                    "Cancel",
                    null,
                    options.ToArray());
                if (response == "Add new pass")
                {
                    await NavigationService.NavigateToAddPassPage(Student.Copy(studentToAdd), new Action(StudentPassAdded), null, classSchedule);
                    return;
                }
                else if (response == "Use another pass")
                {
                    var pass = await popupService.DisplayActionSheet("Select pass", "Cancel", null, studentToAdd.Passes.Where(p => !p.IsExpired).Select(p => p.ToString()).ToArray());

                    if (pass is null || pass == "Cancel")
                    {
                        return;
                    }

                    currentSelectedPass = studentToAdd.Passes.First(p => p.ToString().Equals(pass));
                }
                else if (response == "Use expired pass")
                {
                    var pass = await popupService.DisplayActionSheet("Select pass", "Cancel", null, studentToAdd.Passes.Where(p => p.IsExpired).Select(p => p.ToString()).ToArray());

                    if (pass is null || pass == "Cancel")
                    {
                        return;
                    }

                    currentSelectedPass = studentToAdd.Passes.First(p => p.ToString().Equals(pass));
                }
                else
                {
                    return;
                }
            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            //try
            //{
                var classRollEntry = new ClassRollEntry(studentToAdd, currentSelectedPass);
                await databaseManager.ClassesService.AddStudentToClassRollAsync(CancellationToken.None, classRollId, classRollEntry);
                SearchedStudents.Remove(studentToAdd);
                MarkedStudents.Add(new ClassRollEntry(studentToAdd, currentSelectedPass));
                Search = null;
                RequestSearchBarFocus.Invoke(this, new());
            //}
            //catch (Exception ex)
            //{
            //    await popupService.DisplayAlert("Error", $"An error occured when attempting to add the specified student.\n{ex.Message}.","Ok");
            //}
        }

        private async void StudentPassAdded()
        {
            if (studentToAdd is null)
                return;

            //try
            //{
                await databaseManager.StudentsService.PopulateStudentsFields(CancellationToken.None, studentToAdd);
                var pass = studentToAdd?.Passes?.FirstOrDefault();
                if (pass is null)
                {
                    return;
                }
                var classRollEntry = new ClassRollEntry(studentToAdd, pass);
                await databaseManager.ClassesService.AddStudentToClassRollAsync(CancellationToken.None, classRollId, classRollEntry);
#pragma warning disable CS8604 // Possible null reference argument.
                SearchedStudents.Remove(studentToAdd);
#pragma warning restore CS8604 // Possible null reference argument.
                MarkedStudents.Add(classRollEntry);
                Search = null;
                RequestSearchBarFocus.Invoke(this, new());
            //}
            //catch (TaskCanceledException)
            //{
            //    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            //}
            //catch (Exception e)
            //{
            //    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            //}
        }

        private async void SearchStudentsCommandExecute(object searchQuery)
        {
            var search = ((string)searchQuery).Trim();

            if (search.Length == 0)
            {
                return;
            }

            //try
            //{
                var _students = await databaseManager.StudentsService.SearchStudentsAsync(CancellationToken.None, search, markedStudents.Select(markedStudent => markedStudent.Student), greedyLoad: false, limit: 25);

                if (_students.Count == 1 && _students.FirstOrDefault()?.FirstName.Length < search.Length && _students.FirstOrDefault()?.FullName.ToLower().StartsWith(search.ToLower()) == true)
                {
                    SearchFeedback = "No more students returned";
                    SearchedStudents = new(_students);
                    await AddStudentToRoll(_students.First());
                }
                else if (_students.Count != 0)
                {
                    SearchFeedback = "No more students returned";
                    SearchedStudents = new(_students);
                }
                else
                {
                    SearchedStudents.Clear();
                    SearchFeedback = "No students returned from query";
                }
            //}
            //catch (TaskCanceledException)
            //{
            //    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            //}
            //catch (Exception e)
            //{
            //    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            //}
        }

        public Command SearchStudentsCommand { get; set; }
        public Command AddStudentCommand { get; set; }
        public Command RemoveStudentCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand CancelCommand { get; private set; }
        public RelayCommand SaveCommandCanExecuteChangedCommand { get; private set; }
        public Command UpdatePassCommand { get; private set; }
    }
}
