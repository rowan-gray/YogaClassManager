using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.SQLite.Tests.SqliteSpecificTests;

/// <summary>ApplyChangesAsync is the Mark Roll window's save: if any part of it fails, none of it may
/// stick. The in-memory backend has nothing that can fail part-way, so this is SQLite-only; a foreign-key
/// violation on the last entry stands in for any mid-save failure.</summary>
public class ClassRollAtomicityTests : IAsyncLifetime
{
    private readonly SqliteRepositoryTestFactory factory = new();

    public Task InitializeAsync()
    {
        return factory.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        return factory.DisposeAsync().AsTask();
    }

    private async Task<Student> CreateStudentAsync(string firstName)
    {
        var id = await factory.Students.AddAsync(
            new Student(0, firstName, "Student", null, $"{firstName}.{Guid.NewGuid():N}@example.com".ToLowerInvariant(), true));
        return (await factory.Students.Query(new StudentFilter { Id = (uint)id }).LoadSingle())!;
    }

    [Fact]
    public async Task ApplyChangesAsync_AppliesNothing_WhenAnyChangeFails()
    {
        var scheduleId = await factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)scheduleId }).LoadSingle())!;
        var alice = await CreateStudentAsync("Alice");
        var bob = await CreateStudentAsync("Bob");
        var carol = await CreateStudentAsync("Carol");
        var passId = await factory.Passes.AddAsync(new CasualPass(0, alice.Id, 10, new ObservableCollection<PassAlteration>(), 0));
        var alicePass = (await factory.Passes.GetByIdAsync(passId))!;

        var originalDate = DateOnly.FromDateTime(DateTime.Now);
        var rollId = await factory.ClassRolls.AddAsync(new ClassRoll(0, originalDate, schedule,
            [new ClassRollEntry(alice, alicePass), new ClassRollEntry(bob, null)]));

        // Every earlier change is valid; only the last entry (a student that doesn't exist) fails.
        var ghost = new Student(999_999, "Ghost", "Student", null, null, true);
        await Assert.ThrowsAsync<SqliteException>(() => factory.ClassRolls.ApplyChangesAsync(
            new ClassRoll(rollId, originalDate.AddDays(-3), schedule, []),
            [bob.Id],
            [new ClassRollEntry(alice, null), new ClassRollEntry(carol, null), new ClassRollEntry(ghost, null)]));

        var roll = (await factory.ClassRolls.Query(new ClassRollFilter { Id = (uint)rollId }).LoadSingle())!;
        Assert.Equal(originalDate, roll.Date);
        Assert.Equal([alice.Id, bob.Id], roll.StudentEntries.Select(e => e.Student.Id).Order().ToArray());
        Assert.Equal(alicePass.Id, roll.StudentEntries.Single(e => e.Student.Id == alice.Id).Pass?.Id);
        Assert.Equal(1, (await factory.Passes.GetByIdAsync(alicePass.Id))!.ClassesUsed);
    }
}
