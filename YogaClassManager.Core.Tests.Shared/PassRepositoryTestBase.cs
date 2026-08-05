using System.Collections.ObjectModel;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests.Shared;

public abstract class PassRepositoryTestBase<TFactory> : IAsyncLifetime
    where TFactory : IRepositoryTestFactory, new()
{
    protected TFactory Factory { get; private set; } = default!;

    public virtual async Task InitializeAsync()
    {
        Factory = new TFactory();
        await Factory.InitializeAsync();
    }

    public virtual Task DisposeAsync()
    {
        return Factory.DisposeAsync().AsTask();
    }

    private async Task<int> CreateStudentAsync(string firstName = "Test", string lastName = "Student")
    {
        return await Factory.Students.AddAsync(
            new Student(0, firstName, lastName, null, $"{firstName}.{lastName}.{Guid.NewGuid():N}@example.com".ToLowerInvariant(), true));
    }

    [Fact]
    public async Task UpdateAsync_PersistsCasualPassClassCountChange()
    {
        // The original MAUI app's SavePassAsync never persisted edits to CasualPass.ClassCount - this
        // is the explicit regression test that Core's repository does not reproduce that bug. The
        // in-memory backend also asserts C# reference identity is preserved (Assert.Same) - that
        // assertion is intentionally dropped here since it's meaningless for a SQL-backed model, where
        // every Load produces a fresh object graph; the behavioral guarantee this test exists to
        // protect (the edit is actually persisted and reloads correctly) is what remains.
        var studentId = await CreateStudentAsync();
        var passId = await Factory.Passes.AddAsync(
            new CasualPass(0, studentId, 5, new ObservableCollection<PassAlteration>(), 0));

        var edited = new CasualPass(passId, studentId, 8, new ObservableCollection<PassAlteration>(), 0);
        await Factory.Passes.UpdateAsync(edited);

        var reloaded = await Factory.Passes.GetByIdAsync(passId);
        Assert.IsType<CasualPass>(reloaded);
        Assert.Equal(8, ((CasualPass)reloaded!).ClassCount);
    }

    [Fact]
    public async Task AddAndRemoveAlteration_UpdatesClassesRemainingAndIsDepleted()
    {
        var studentId = await CreateStudentAsync();
        var passId = await Factory.Passes.AddAsync(
            new CasualPass(0, studentId, 2, new ObservableCollection<PassAlteration>(), 0));
        // ClassesUsed is a derived value (from real class-roll attendance), not a stored/settable
        // field - simulate "already used 2 of 2" via the seed hook rather than a constructor argument.
        await Factory.SeedPassUsageAsync(passId, studentId, usesCount: 2);

        var depleted = await Factory.Passes.GetByIdAsync(passId);
        Assert.True(depleted!.IsDepleted);

        var alteration = new PassAlteration(0, passId, 3, "Bonus classes");
        await Factory.Passes.AddAlterationAsync(alteration);

        var withAlteration = await Factory.Passes.GetByIdAsync(passId);
        Assert.False(withAlteration!.IsDepleted);
        Assert.Equal(3, withAlteration.ClassesRemaining);

        await Factory.Passes.RemoveAlterationAsync(alteration.Id);
        var withoutAlteration = await Factory.Passes.GetByIdAsync(passId);
        Assert.True(withoutAlteration!.IsDepleted);
    }

    [Fact]
    public async Task GetUsageHistoryAsync_ReturnsEveryRollThePassWasUsedFor()
    {
        var studentId = await CreateStudentAsync("Alice", "Johnson");
        var today = DateOnly.FromDateTime(DateTime.Now);
        var passId = await Factory.Passes.AddAsync(new DatedPass(0, studentId, 10, new ObservableCollection<PassAlteration>(), 2,
            today.AddDays(-10), today.AddDays(10)));

        var student = (await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle())!;
        var pass = await Factory.Passes.GetByIdAsync(passId);

        var scheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)scheduleId }).LoadSingle())!;

        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-7), schedule, [new ClassRollEntry(student, pass)]));
        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule, [new ClassRollEntry(student, pass)]));
        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-14), schedule, [new ClassRollEntry(student, null)]));

        var history = await Factory.Passes.GetUsageHistoryAsync(passId);

        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.Equal(passId, r.PassId));
    }
}
