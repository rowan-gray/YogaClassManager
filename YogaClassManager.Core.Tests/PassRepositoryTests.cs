using System.Collections.ObjectModel;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests;

public class PassRepositoryTests
{
    private static (InMemoryDataStore store, InMemoryPassRepository repo) CreateRepo()
    {
        var store = new InMemoryDataStore();
        return (store, new InMemoryPassRepository(store));
    }

    [Fact]
    public async Task UpdateAsync_PersistsCasualPassClassCountChange()
    {
        // The original MAUI app's SavePassAsync never persisted edits to CasualPass.ClassCount - this
        // is the explicit regression test that the dummy/Core repository does not reproduce that bug.
        var (store, repo) = CreateRepo();
        var pass = new CasualPass(store.NextId(), 1, 5, new ObservableCollection<PassAlteration>(), 0);
        store.Passes[pass.Id] = pass;

        var edited = new CasualPass(pass.Id, 1, 8, new ObservableCollection<PassAlteration>(), 0);
        await repo.UpdateAsync(edited);

        var reloaded = await repo.GetByIdAsync(pass.Id);
        Assert.IsType<CasualPass>(reloaded);
        Assert.Equal(8, ((CasualPass)reloaded!).ClassCount);
        Assert.Same(pass, reloaded); // identity preserved, not replaced wholesale
    }

    [Fact]
    public async Task AddAndRemoveAlteration_UpdatesClassesRemainingAndIsDepleted()
    {
        var (store, repo) = CreateRepo();
        var pass = new CasualPass(store.NextId(), 1, 2, new ObservableCollection<PassAlteration>(), 2); // depleted
        store.Passes[pass.Id] = pass;
        Assert.True(pass.IsDepleted);

        var alteration = new PassAlteration(0, pass.Id, 3, "Bonus classes");
        await repo.AddAlterationAsync(alteration);

        Assert.False(pass.IsDepleted);
        Assert.Equal(3, pass.ClassesRemaining);

        await repo.RemoveAlterationAsync(alteration.Id);
        Assert.True(pass.IsDepleted);
    }

    [Fact]
    public async Task GetUsageHistoryAsync_ReturnsEveryRollThePassWasUsedFor()
    {
        var (store, _) = CreateRepo();
        var passRepo = new InMemoryPassRepository(store);

        var student = new Student(store.NextId(), "Alice", "Johnson", null, null, true);
        var pass = new DatedPass(store.NextId(), student.Id, 10, new ObservableCollection<PassAlteration>(), 2,
            DateOnly.FromDateTime(DateTime.Now).AddDays(-10), DateOnly.FromDateTime(DateTime.Now).AddDays(10));
        store.People[student.Id] = student;
        store.Passes[pass.Id] = pass;

        var schedule = new ClassSchedule(store.NextId(), DayOfWeek.Monday, new TimeOnly(9, 0), false);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var roll1 = new ClassRoll(store.NextId(), today.AddDays(-7), schedule, [new ClassRollEntry(student, pass)]);
        var roll2 = new ClassRoll(store.NextId(), today, schedule, [new ClassRollEntry(student, pass)]);
        var rollWithoutThisPass = new ClassRoll(store.NextId(), today.AddDays(-14), schedule,
            [new ClassRollEntry(student, null)]);
        store.ClassRolls[roll1.Id] = roll1;
        store.ClassRolls[roll2.Id] = roll2;
        store.ClassRolls[rollWithoutThisPass.Id] = rollWithoutThisPass;

        var history = await passRepo.GetUsageHistoryAsync(pass.Id);

        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.Equal(pass.Id, r.PassId));
    }
}
