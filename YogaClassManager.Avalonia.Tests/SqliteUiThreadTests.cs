using System.Collections.Specialized;
using System.Reactive.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Dapper;
using Microsoft.Data.Sqlite;
using YogaClassManager.Avalonia.Services;
using System.ComponentModel;
using YogaClassManager.Avalonia.ViewModels.ClassRolls;
using YogaClassManager.Avalonia.ViewModels.Dashboard;
using YogaClassManager.Avalonia.ViewModels.Identities;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Avalonia.ViewModels.Students;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.SQLite.Repositories;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     The SQLite store runs queries on the thread pool (so the UI can paint while they run), which
///     means every await on a repository call now genuinely yields. Everything a view binds to must
///     still only ever be changed on the UI thread - a background-thread change crashed the app
///     ("Call from invalid thread"). The in-memory repositories complete synchronously and can't catch
///     this, so this runs a real page against the real SQLite repositories.
/// </summary>
public class SqliteUiThreadTests
{
    private sealed class Provider(SqliteDataStore store) : IDataStoreProvider
    {
        public SqliteDataStore RetrieveDataStore() => store;
    }

    private static void PumpUntil(Func<bool> done, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!done() && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A fresh shared-cache in-memory database (kept alive by the returned connection), with
    /// two students who each have a pass and an attended class.</summary>
    private static async Task<(SqliteConnection KeepAlive, SqliteDataStore Store)> OpenSeededStoreAsync()
    {
        var source = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(source);
        await keepAlive.OpenAsync();
        var store = await SqliteDataStore.OpenAsync(source);

        await store.ExecuteInTransactionAsync((c, t) => c.ExecuteAsync(
            """
            INSERT INTO Person(PersonId, FirstName, LastName, PhoneNumber) VALUES (1, 'Ada', 'L', '0400000000'), (2, 'Grace', 'H', '0411111111');
            INSERT INTO Student(StudentId) VALUES (1), (2);
            INSERT INTO ClassSchedule(ClassScheduleId, Day, Time, IsArchived) VALUES (1, 1, 540, 0);
            INSERT INTO Pass(PassId, StudentId) VALUES (1, 1), (2, 2);
            INSERT INTO DatedPass(PassId, ClassCount, StartDate, EndDate) VALUES (1, 10, '2026-01-01', '2026-12-31'), (2, 10, '2026-01-01', '2026-12-31');
            INSERT INTO ClassRoll(ClassId, ClassScheduleId, Date) VALUES (1, 1, '2026-11-16');
            INSERT INTO ClassStudents(ClassId, StudentId, PassId) VALUES (1, 1, 1), (1, 2, 2);
            INSERT INTO StudentHealthConcerns(StudentId, HealthConcern) VALUES (1, 'Asthma');
            """, transaction: t));

        return (keepAlive, store);
    }

    [AvaloniaFact]
    public async Task StudentsPage_OnlyChangesBoundListsOnTheUiThread()
    {
        var (keepAlive, store) = await OpenSeededStoreAsync();
        await using var _ = keepAlive;
        await using var __ = store;

        var provider = new Provider(store);
        var identities = new SqliteIdentityRepository(provider);
        var passes = new SqlitePassRepository(provider);
        var students = new SqliteStudentRepository(provider, identities, passes, new SqliteEmergencyContactRepository(provider));
        var viewModel = new StudentsViewModel(new AppScreen(), students, identities, passes,
            new SqliteTermRepository(provider), new SqliteClassRollRepository(provider), new ScriptedDialogService(),
            new FakeToastService(), new FakeNavigator());

        var offThread = new List<string>();
        void Watch(INotifyCollectionChanged collection, string name) =>
            collection.CollectionChanged += (_, _) =>
            {
                if (!Dispatcher.UIThread.CheckAccess())
                    offThread.Add($"{name} on thread {Environment.CurrentManagedThreadId}");
            };
        Watch(viewModel.Items, nameof(viewModel.Items));
        Watch(viewModel.Passes, nameof(viewModel.Passes));
        Watch(viewModel.AttendanceHistory, nameof(viewModel.AttendanceHistory));
        Watch(viewModel.EmergencyContacts, nameof(viewModel.EmergencyContacts));
        Watch(viewModel.HealthConcerns, nameof(viewModel.HealthConcerns));

        PumpUntil(() => viewModel.Items.Count == 2 && !viewModel.IsLoadingDetails && viewModel.AttendanceHistory.Count > 0);
        viewModel.Selection = viewModel.Items[1];
        PumpUntil(() => !viewModel.IsLoadingDetails && viewModel.Passes.Count > 0);
        Assert.Empty(offThread);
        Assert.NotEmpty(viewModel.Passes);
    }

    /// <summary>The Dashboard's "Search student" quick action: pick a student, land on their page. The
    /// Students page is built by that navigation, after the picker's search has awaited the database -
    /// this crashed the app when the page's loads ran (and wrote to bound lists) off the UI thread.</summary>
    [AvaloniaFact]
    public async Task SearchStudentQuickAction_OnlyChangesBoundListsOnTheUiThread()
    {
        var (keepAlive, store) = await OpenSeededStoreAsync();
        await using var _ = keepAlive;
        await using var __ = store;

        var provider = new Provider(store);
        var identities = new SqliteIdentityRepository(provider);
        var passes = new SqlitePassRepository(provider);
        var students = new SqliteStudentRepository(provider, identities, passes, new SqliteEmergencyContactRepository(provider));
        var terms = new SqliteTermRepository(provider);
        var rolls = new SqliteClassRollRepository(provider);
        var screen = new AppScreen();

        var offThread = new List<string>();
        void Watch(INotifyCollectionChanged collection, string name) =>
            collection.CollectionChanged += (_, _) =>
            {
                if (!Dispatcher.UIThread.CheckAccess())
                    offThread.Add($"{name} on thread {Environment.CurrentManagedThreadId}");
            };

        StudentsViewModel? studentsPage = null;
        PageNavigator navigator = null!;
        navigator = new PageNavigator(screen, type =>
        {
            if (type != typeof(StudentsViewModel))
                return null;
            if (!Dispatcher.UIThread.CheckAccess())
                offThread.Add("StudentsViewModel constructed");
            studentsPage = new StudentsViewModel(screen, students, identities, passes, terms, rolls,
                new ScriptedDialogService(), new FakeToastService(), navigator);
            Watch(studentsPage.Items, nameof(studentsPage.Items));
            Watch(studentsPage.Passes, nameof(studentsPage.Passes));
            Watch(studentsPage.AttendanceHistory, nameof(studentsPage.AttendanceHistory));
            Watch(studentsPage.EmergencyContacts, nameof(studentsPage.EmergencyContacts));
            Watch(studentsPage.HealthConcerns, nameof(studentsPage.HealthConcerns));
            return studentsPage;
        });

        var dialogs = new DialogService();
        var dashboard = new DashboardViewModel(screen, students, new SqliteClassScheduleRepository(provider), rolls,
            identities, passes, terms, new FakeRollWindowService(), dialogs, new FakeToastService(), navigator,
            new NootNootEasterEgg(new FakeSoundPlayer()));

        dashboard.SearchStudentCommand.Execute().Subscribe();
        PumpUntil(() => dialogs.ActiveDialog is StudentPickerViewModel);
        var picker = Assert.IsType<StudentPickerViewModel>(dialogs.ActiveDialog);

        // Choose straight away rather than typing a search: the search's throttle runs on a dispatcher
        // timer the headless platform doesn't fire, and choosing is what starts the navigation anyway.
        var grace = (await students.Query(new() { NameFilter = "Grace" }).LoadMultiple()).Single();
        picker.Search.ChooseCommand.Execute(grace).Subscribe();

        PumpUntil(() => studentsPage is { IsLoadingDetails: false, Selection.FirstName: "Grace" } &&
                        studentsPage.Passes.Count > 0);
        Assert.Equal("Grace", studentsPage?.Selection?.FirstName);
        Assert.Empty(offThread);
    }

    private sealed class ThreadRecordingRollWindows(List<string> offThread) : IRollWindowService
    {
        public List<ClassRoll> Opened { get; } = [];

        public void OpenOrActivate(ClassRoll roll, Action onClosed)
        {
            if (!Dispatcher.UIThread.CheckAccess())
                offThread.Add("roll window opened");
            Opened.Add(roll);
        }

        public void CloseAll()
        {
        }
    }

    /// <summary>Every "go to this record" route (IPageNavigator) builds a page that selects - or for a roll,
    /// opens - the record once its list has loaded. That hand-off, and everything it triggers, must happen
    /// on the UI thread even though the list load finishes on a thread-pool thread.</summary>
    [AvaloniaTheory]
    [InlineData("Student")]
    [InlineData("Identity")]
    [InlineData("ClassRoll")]
    public async Task GoingToARecord_SelectsItOnTheUiThread(string route)
    {
        var (keepAlive, store) = await OpenSeededStoreAsync();
        await using var _ = keepAlive;
        await using var __ = store;

        var provider = new Provider(store);
        var identities = new SqliteIdentityRepository(provider);
        var passes = new SqlitePassRepository(provider);
        var contacts = new SqliteEmergencyContactRepository(provider);
        var students = new SqliteStudentRepository(provider, identities, passes, contacts);
        var terms = new SqliteTermRepository(provider);
        var rolls = new SqliteClassRollRepository(provider);
        var screen = new AppScreen();
        var offThread = new List<string>();
        var rollWindows = new ThreadRecordingRollWindows(offThread);

        IRoutableViewModelWithSelection? page = null;
        PageNavigator navigator = null!;
        navigator = new PageNavigator(screen, type =>
        {
            object? created = type == typeof(StudentsViewModel)
                ? new StudentsViewModel(screen, students, identities, passes, terms, rolls,
                    new ScriptedDialogService(), new FakeToastService(), navigator)
                : type == typeof(IdentitiesViewModel)
                    ? new IdentitiesViewModel(screen, identities, students, contacts,
                        new ScriptedDialogService(), new FakeToastService(), navigator)
                    : type == typeof(ClassRollsViewModel)
                        ? new ClassRollsViewModel(screen, rolls, students, passes, terms,
                            new ScriptedDialogService(), new FakeToastService(), rollWindows, navigator)
                        : null;
            if (created is INotifyPropertyChanged notifying)
                notifying.PropertyChanged += (_, e) =>
                {
                    if (!Dispatcher.UIThread.CheckAccess())
                        offThread.Add($"{e.PropertyName} changed");
                };
            page = created is null ? null : new IRoutableViewModelWithSelection(created);
            return created;
        });

        var arrived = route switch
        {
            "Student" => navigator.ToStudent(2),
            "Identity" => navigator.ToIdentity(2),
            _ => navigator.ToClassRoll(1)
        };
        arrived.Subscribe();

        PumpUntil(() => route == "ClassRoll" ? rollWindows.Opened.Count > 0 : page?.SelectedId == 2);
        PumpUntil(() => page is { IsLoadingDetails: false }, 1000);

        if (route == "ClassRoll")
            Assert.Equal(1, Assert.Single(rollWindows.Opened).Id);
        else
            Assert.Equal(2, page?.SelectedId);
        Assert.Empty(offThread);
    }

    /// <summary>Reads Selection/IsLoadingDetails off whichever list page the navigator built.</summary>
    private sealed class IRoutableViewModelWithSelection(object page)
    {
        private dynamic Page => page;
        public int? SelectedId => (int?)Page.Selection?.Id;
        public bool IsLoadingDetails => Page.IsLoadingDetails;
    }
}
