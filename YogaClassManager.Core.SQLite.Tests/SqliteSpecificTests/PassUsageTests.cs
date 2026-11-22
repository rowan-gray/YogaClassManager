using System.Collections.ObjectModel;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.SQLite.Tests.SqliteSpecificTests;

/// <summary>On SQLite, Pass.ClassesUsed/IsDepleted/IsExpired are derived by the PassStatus view from real
/// ClassStudents attendance rows - not stored. These pin that derivation to the roll-marking write path,
/// and pin the view's IsExpired (used by the IncludeExpired filter) to each pass type's own C# IsExpired.
/// SQLite-only: the in-memory backend's ClassesUsed is a free-standing field that attendance doesn't
/// touch (see IRepositoryTestFactory.SeedPassUsageAsync).</summary>
public class PassUsageTests : IAsyncLifetime
{
    private readonly SqliteRepositoryTestFactory factory = new();
    private readonly DateOnly today = DateOnly.FromDateTime(DateTime.Now);

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

    private async Task<ClassSchedule> CreateScheduleAsync(DayOfWeek day = DayOfWeek.Monday)
    {
        var id = await factory.ClassSchedules.AddAsync(new ClassSchedule(0, day, new TimeOnly(9, 0), false));
        return (await factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)id }).LoadSingle())!;
    }

    private async Task<Pass> AddCasualPassAsync(Student student, int classCount)
    {
        var id = await factory.Passes.AddAsync(
            new CasualPass(0, student.Id, classCount, new ObservableCollection<PassAlteration>(), 0));
        return (await factory.Passes.GetByIdAsync(id))!;
    }

    private async Task<int> ClassesUsedAsync(int passId)
    {
        return (await factory.Passes.GetByIdAsync(passId))!.ClassesUsed;
    }

    [Fact]
    public async Task MarkingAttendance_UsesAClass_AndUnmarkingGivesItBack()
    {
        var alice = await CreateStudentAsync("Alice");
        var schedule = await CreateScheduleAsync();
        var pass = await AddCasualPassAsync(alice, 10);
        var rollId = await factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule, []));

        await factory.ClassRolls.AddStudentEntryAsync(rollId, new ClassRollEntry(alice, pass));
        var afterMarking = (await factory.Passes.GetByIdAsync(pass.Id))!;
        Assert.Equal(1, afterMarking.ClassesUsed);
        Assert.Equal(9, afterMarking.ClassesRemaining);

        await factory.ClassRolls.RemoveStudentEntryAsync(rollId, alice.Id);
        var afterUnmarking = (await factory.Passes.GetByIdAsync(pass.Id))!;
        Assert.Equal(0, afterUnmarking.ClassesUsed);
        Assert.Equal(10, afterUnmarking.ClassesRemaining);
    }

    [Fact]
    public async Task AttendingWithoutAPass_DoesNotUseAnyPass()
    {
        var alice = await CreateStudentAsync("Alice");
        var schedule = await CreateScheduleAsync();
        var pass = await AddCasualPassAsync(alice, 10);

        await factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule, [new ClassRollEntry(alice, null)]));

        Assert.Equal(0, await ClassesUsedAsync(pass.Id));
    }

    [Fact]
    public async Task ChangingTheAttendeesPass_MovesTheUsageToTheNewPass()
    {
        var alice = await CreateStudentAsync("Alice");
        var schedule = await CreateScheduleAsync();
        var first = await AddCasualPassAsync(alice, 10);
        var second = await AddCasualPassAsync(alice, 5);
        var rollId = await factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule, [new ClassRollEntry(alice, first)]));

        await factory.ClassRolls.UpdateStudentEntryPassAsync(rollId, alice.Id, second.Id);
        Assert.Equal(0, await ClassesUsedAsync(first.Id));
        Assert.Equal(1, await ClassesUsedAsync(second.Id));

        await factory.ClassRolls.UpdateStudentEntryPassAsync(rollId, alice.Id, null);
        Assert.Equal(0, await ClassesUsedAsync(second.Id));
    }

    /// <summary>Re-adding a student already on the roll replaces their entry, so it must not count twice.</summary>
    [Fact]
    public async Task ReAddingAnAttendee_DoesNotUseTheirPassTwice()
    {
        var alice = await CreateStudentAsync("Alice");
        var schedule = await CreateScheduleAsync();
        var pass = await AddCasualPassAsync(alice, 10);
        var rollId = await factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule, [new ClassRollEntry(alice, pass)]));

        await factory.ClassRolls.AddStudentEntryAsync(rollId, new ClassRollEntry(alice, pass));

        Assert.Equal(1, await ClassesUsedAsync(pass.Id));
    }

    [Fact]
    public async Task DeletingARoll_GivesBackTheClassesItUsed()
    {
        var alice = await CreateStudentAsync("Alice");
        var bob = await CreateStudentAsync("Bob");
        var schedule = await CreateScheduleAsync();
        var alicePass = await AddCasualPassAsync(alice, 10);
        var bobPass = await AddCasualPassAsync(bob, 10);
        var rollId = await factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule,
            [new ClassRollEntry(alice, alicePass), new ClassRollEntry(bob, bobPass)]));
        await factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-7), schedule, [new ClassRollEntry(alice, alicePass)]));

        await factory.ClassRolls.DeleteAsync(rollId);

        Assert.Equal(1, await ClassesUsedAsync(alicePass.Id));
        Assert.Equal(0, await ClassesUsedAsync(bobPass.Id));
    }

    [Fact]
    public async Task APass_BecomesDepleted_ExactlyWhenItsLastClassIsUsed()
    {
        var alice = await CreateStudentAsync("Alice");
        var schedule = await CreateScheduleAsync();
        var pass = await AddCasualPassAsync(alice, 2);

        await factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-7), schedule, [new ClassRollEntry(alice, pass)]));
        var oneLeft = (await factory.Passes.GetByIdAsync(pass.Id))!;
        Assert.False(oneLeft.IsDepleted);
        Assert.Contains(await factory.Students.GetPassesAsync(alice.Id), p => p.Id == pass.Id);

        await factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule, [new ClassRollEntry(alice, pass)]));
        var noneLeft = (await factory.Passes.GetByIdAsync(pass.Id))!;
        Assert.True(noneLeft.IsDepleted);
        Assert.Equal(0, noneLeft.ClassesRemaining);
        Assert.DoesNotContain(await factory.Students.GetPassesAsync(alice.Id), p => p.Id == pass.Id);
        Assert.Contains(await factory.Students.GetPassesAsync(alice.Id, includeDepleted: true), p => p.Id == pass.Id);
    }

    /// <summary>The view's IsExpired (behind the IncludeExpired filter) and each pass type's C# IsExpired
    /// are two copies of the same rule; they must agree at every boundary - a pass is still usable on its
    /// last day, and a term pass lasts until its catchup end date when the term has one.</summary>
    [Fact]
    public async Task ExpiryFilter_AgreesWithEachPassTypesIsExpired_AtTheBoundaries()
    {
        var alice = await CreateStudentAsync("Alice");
        var noAlterations = () => new ObservableCollection<PassAlteration>();

        var datedEndsToday = await factory.Passes.AddAsync(
            new DatedPass(0, alice.Id, 5, noAlterations(), 0, today.AddDays(-30), today));
        var datedEndedYesterday = await factory.Passes.AddAsync(
            new DatedPass(0, alice.Id, 5, noAlterations(), 0, today.AddDays(-30), today.AddDays(-1)));
        var termInCatchup = await AddTermPassAsync(alice, "In catchup", today.AddDays(-60), today.AddDays(-1),
            today.AddDays(-70), today, DayOfWeek.Monday);
        var termEndedNoCatchup = await AddTermPassAsync(alice, "Ended", today.AddDays(-60), today.AddDays(-1),
            null, null, DayOfWeek.Tuesday);
        var termEndsToday = await AddTermPassAsync(alice, "Ends today", today.AddDays(-60), today,
            null, null, DayOfWeek.Wednesday);
        var casual = (await AddCasualPassAsync(alice, 5)).Id;

        var expected = new Dictionary<int, bool>
        {
            [datedEndsToday] = false,
            [datedEndedYesterday] = true,
            [termInCatchup] = false,
            [termEndedNoCatchup] = true,
            [termEndsToday] = false,
            [casual] = false
        };

        var all = await factory.Students.GetPassesAsync(alice.Id, includeExpired: true, includeDepleted: true);
        var notExpired = (await factory.Students.GetPassesAsync(alice.Id, includeDepleted: true)).Select(p => p.Id).ToHashSet();

        Assert.Equal(expected.Count, all.Count);
        foreach (var pass in all)
        {
            Assert.True(expected[pass.Id] == pass.IsExpired, $"{pass}: C# IsExpired was {pass.IsExpired}");
            Assert.True(expected[pass.Id] == !notExpired.Contains(pass.Id),
                $"{pass}: IncludeExpired=false filter {(notExpired.Contains(pass.Id) ? "kept" : "dropped")} it");
        }
    }

    private async Task<int> AddTermPassAsync(Student student, string name, DateOnly start, DateOnly end,
        DateOnly? catchupStart, DateOnly? catchupEnd, DayOfWeek day)
    {
        var schedule = await CreateScheduleAsync(day);
        var termId = await factory.Terms.AddAsync(new Term(0, name, start, end, catchupStart, catchupEnd, []));
        await factory.Terms.LinkClassAsync(termId, schedule.Id, 10);

        var term = (await factory.Terms.Query(new TermFilter { Id = (uint)termId, IncludeCompleted = true }).LoadSingle())!;
        var termClass = term.Classes.Single(c => c.ClassSchedule.Id == schedule.Id);

        return await factory.Passes.AddAsync(
            new TermPass(0, student.Id, 0, new ObservableCollection<PassAlteration>(), term, termClass));
    }
}
