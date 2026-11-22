using System.Collections.Specialized;
using System.Reactive.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Dapper;
using Microsoft.Data.Sqlite;
using YogaClassManager.Avalonia.ViewModels.Students;
using YogaClassManager.Core.SQLite.Data;
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

    [AvaloniaFact]
    public async Task StudentsPage_OnlyChangesBoundListsOnTheUiThread()
    {
        var source = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var keepAlive = new SqliteConnection(source);
        await keepAlive.OpenAsync();
        await using var store = await SqliteDataStore.OpenAsync(source);

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
}
