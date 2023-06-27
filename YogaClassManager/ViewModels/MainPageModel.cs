using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.Classes;
using YogaClassManager.Models.People;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    public partial class MainPageModel : BasePageModel
    {
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;
        private Random random = new();

        [ObservableProperty]
        private string headerText;
        [ObservableProperty]
        private string quote;

        public Command AddStudentCommand { get; init; }
        public Command AddPassCommand { get; init; }
        public Command CheckPassesCommand { get; init; }
        public Command EditLastClassCommand { get; init; }
        public Command HeaderPressedCommand { get; init; }
        public Command ResetPageCommand { get; init; }

        [ObservableProperty]
        ObservableCollection<QuickActionItem> quickActions;

        [ObservableProperty]
        ObservableCollection<QuickActionItem> todaysClasses;
        private List<string> quotes;

        public MainPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            AddStudentCommand = new(AddStudentCommandExecute);
            AddPassCommand = new(AddPassCommandExecute);
            CheckPassesCommand = new(CheckPassesCommandExecute);
            EditLastClassCommand = new(EditLastClassCommandExecute);
            HeaderPressedCommand = new(HeaderPressedCommandExecute);
            ResetPageCommand = new(PopulateTodaysClasses);
            QuickActions = new()
            {
                new(AddStudentCommand, "Add Student", "add_person.png"),
                new(AddPassCommand, "Add Pass", "add_symbol.png"),
                new(CheckPassesCommand, "Check Passes Remaining", "search.png"),
                new(EditLastClassCommand, "Edit Previous Class", "edit_calendar.png")
            };
            TodaysClasses = new();

            HeaderText = GetHeaderText();

            SetQuote();
        }

        private async void SetQuote()
        {
            quotes = await ReadQuotes();

            Quote = quotes[(new Random()).Next(quotes.Count)];
        }

        private async Task<List<string>> ReadQuotes()
        {
            using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("quotes.txt");
            using StreamReader reader = new StreamReader(fileStream);

            var lines = new List<string>();

            while (!reader.EndOfStream)
            {
                lines.Add(await reader.ReadLineAsync());
            }

            return lines.ToList();
        }

        private string GetHeaderText()
        {
            var currentTime = DateTime.Now;

            if (currentTime.Hour > 18)
            {
                return "Good evening Tracy";
            }
            else if (currentTime.Hour > 10)
            {
                return "Have a good day Tracy";
            }
            if (currentTime.Hour > 5)
            {
                return "Good morning Tracy";
            }
            else
            {
                return "Good night Tracy";
            }
        }

        private async void HeaderPressedCommandExecute()
        {
            List<string> soundFiles = new List<string>() { "noot_noot.mp3", "noot-noot.mp3", "noot-3453.mp3", "noot-3459.mp3", "noot-3457.mp3", "noot-3452.mp3", "noot-3451.mp3", "noot-3450.mp3", "noot-noot-and-laugh.mp3", "pingu-music.mp3" };

            var audioPlayer = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync(soundFiles[random.Next(0, soundFiles.Count)]));

            audioPlayer.Play();
        }

        private async void PopulateTodaysClasses()
        {
            StartBusy();
            try
            {
                try
                {
                    List<ClassSchedule> classes;
                    try
                    {
                        classes = await databaseManager.ClassesService.GetTodaysClassesAsync(CancellationToken.None);
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

                    TodaysClasses.Clear();

                    foreach (var c in classes)
                    {
                        Command command;
                        string image;
                        try
                        {
                            var classRoll = await databaseManager.ClassesService.GetTodaysClassRollFromClassAsync(CancellationToken.None, c);
                            command = new Command(async () => await NavigationService.NavigateTo(nameof(MarkRollPageModel), c, null, classRoll));
                            image = "edit_calendar.png";
                        }
                        catch (ArgumentException)
                        {
                            command = new Command(async () => await NavigationService.NavigateTo(nameof(MarkRollPageModel), c, null));
                            image = "add_calendar.png";
                        }

                        TodaysClasses.Add(new QuickActionItem(
                            command,
                            c.ToString(),
                            image));
                    }
                }
                catch (ArgumentException)
                {
                    // no classes today;
                    TodaysClasses.Clear();
                }
            }
            catch (IOException)
            {
                await Task.Delay(2000);
                await popupService.DisplayAlert("File not valid", "There was an error when attempting to read from the database.\nPlease check the location of the database in the settings.", "Okay");
                //NavigationService.NavigateToSettingsPage();
            }
            finally
            {
                EndBusy();
            }
        }

        private async void AddStudentCommandExecute()
        {
            await NavigationService.NavigateToAddStudentPage();
        }
        private async void EditLastClassCommandExecute()
        {
            StartBusy();
            try
            {
                var lastClass = await databaseManager.ClassesService.GetLastClassRollAsync(CancellationToken.None);
                await NavigationService.NavigateTo(nameof(MarkRollPageModel), lastClass.ClassSchedule, null, lastClass);
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            }
            catch (ArgumentException)
            {
                await popupService.DisplayAlert("No Class", "There is no previous class to retrieve.", "Ok");
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }
            finally
            {
                EndBusy();
            }
        }

        private async void AddPassCommandExecute()
        {
            Student student;
            try
            {
                student = await SelectStudent("Who would you like to add a pass to?");

            }
            catch
            {
                return;
            }

            if (student is not null)
                await NavigationService.NavigateToAddPassPage(student);
        }
        private async void CheckPassesCommandExecute()
        {
            Student student;
            try
            {
                student = await SelectStudent("Who would you like to add a pass to?");

            }
            catch
            {
                return;
            }

            if (student is not null)
            {
                StartBusy();
                try
                {
                    List<Pass> passes;
                    try
                    {
                        passes = await databaseManager.StudentsService.GetStudentsPasses(CancellationToken.None, student.Id, false);
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

                    if (passes.Count == 0)
                    {
                        await popupService.DisplayAlert("No Passes", "This student does not have any passes.", "Ok");
                        return;
                    }
                    else if (passes.Count > 1)
                    {
                        var sumation = 0;
                        List<string> indivualPassCounts = new();
                        foreach (var pass in passes)
                        {
                            sumation += pass.ClassesRemaining;
                            indivualPassCounts.Add($"{pass} with {pass.ClassesRemaining} classes remaining.");
                        }

                        await popupService.DisplayAlert("Passes Remaining", $"{student.FullName} has {sumation} total classes remaining " +
                            $"across {passes.Count} passes.\n\n" + 
                            String.Join("\n", indivualPassCounts), "Ok");

                        return;
                    }
                    else
                    {
                        var selectedPass = passes.First();
                        await popupService.DisplayAlert("Passes Remaining",
                            $"{student.FullName} has a {selectedPass} with {selectedPass.ClassesRemaining} classes remaining.",
                            "Ok");
                    }
                }
                finally
                {
                    EndBusy();
                }
            }
        }

        private async Task<Student> SelectStudent(string message)
        {
            StartBusy();
            try
            {

                var studentName = await popupService.DisplayPromptAsync("Student Name", message, "Search");

                if (studentName is null)
                {
                    return null;
                }

                List<Student> students;
                try
                {
                    students = await databaseManager.StudentsService.SearchStudentsAsync(CancellationToken.None, studentName, null);
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


                if (students.Count == 0)
                {
                    await popupService.DisplayAlert("Invalid Name", "There are no students matching that name.", "Ok");
                    return null;
                }
                else if (students.Count > 1)
                {
                    List<string> studentNames = new();
                    foreach (var student in students)
                    {
                        studentNames.Add(student.FullName);
                    }

                    var selectedStudentName = await popupService.DisplayActionSheet("Select Student", "Cancel", null, studentNames.ToArray());

                    if (selectedStudentName == "Cancel")
                    {
                        return null;
                    }

                    return students.First(student => student.FullName == selectedStudentName);
                }
                else
                {
                    return students.First();
                }
            }
            finally
            {
                EndBusy();
            }
        }
    }
}
