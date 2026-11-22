using System.Collections.ObjectModel;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Core.Tests.Shared;

public abstract class ClassRollRepositoryTestBase<TFactory> : IAsyncLifetime
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

    private async Task<(ClassSchedule mon, ClassSchedule wed, Student student)> CreateFixtureAsync()
    {
        var monId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var wedId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Wednesday, new TimeOnly(18, 0), false));
        var mon = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)monId }).LoadSingle())!;
        var wed = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)wedId }).LoadSingle())!;

        var studentId = await Factory.Students.AddAsync(new Student(0, "Alice", "Johnson", null, "alice@example.com", true));
        var student = (await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle())!;

        var today = DateOnly.FromDateTime(DateTime.Now);
        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-14), mon, [new ClassRollEntry(student, null)]));
        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-1), wed, [new ClassRollEntry(student, null)]));

        return (mon, wed, student);
    }

    [Fact]
    public async Task Query_FiltersByDayOfWeekAndDateRange()
    {
        var (mon, _, _) = await CreateFixtureAsync();

        var mondayOnly = await Factory.ClassRolls.Query(new ClassRollFilter { DayOfWeek = DayOfWeek.Monday }).LoadMultiple();
        Assert.Single(mondayOnly);
        Assert.Equal(mon.Id, mondayOnly[0].ClassSchedule.Id);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var lastThreeDays = await Factory.ClassRolls.Query(new ClassRollFilter { DateFrom = today.AddDays(-3) }).LoadMultiple();
        Assert.Single(lastThreeDays);
    }

    [Fact]
    public async Task Query_SortsByDateAscendingAndDescending()
    {
        await CreateFixtureAsync();

        var ascending = await Factory.ClassRolls.Query(new ClassRollFilter
        {
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Ascending)
        }).LoadMultiple();
        Assert.True(ascending[0].Date < ascending[1].Date);

        var descending = await Factory.ClassRolls.Query(new ClassRollFilter
        {
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Descending)
        }).LoadMultiple();
        Assert.True(descending[0].Date > descending[1].Date);
    }

    [Fact]
    public async Task GetAttendanceHistoryAsync_ReturnsEveryRollTheStudentAttended()
    {
        var (_, _, student) = await CreateFixtureAsync();

        var history = await Factory.ClassRolls.GetAttendanceHistoryAsync(student.Id);

        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.Equal(student.Id, r.StudentId));
    }

    // The following three mirror Query_FiltersByDayOfWeekAndDateRange/Query_SortsByDateAscendingAndDescending
    // above, but through GetAttendanceHistoryAsync's own ClassRollFilter parameter - this list's
    // filtering/ordering must be the Class Rolls list's, not a parallel implementation of it.

    [Fact]
    public async Task GetAttendanceHistoryAsync_FiltersByDayOfWeekAndDateRange()
    {
        var (mon, _, student) = await CreateFixtureAsync();

        var mondayOnly = await Factory.ClassRolls.GetAttendanceHistoryAsync(student.Id,
            new ClassRollFilter { DayOfWeek = DayOfWeek.Monday });
        Assert.Single(mondayOnly);
        Assert.Equal(mon.Id, mondayOnly[0].ClassSchedule.Id);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var lastThreeDays = await Factory.ClassRolls.GetAttendanceHistoryAsync(student.Id,
            new ClassRollFilter { DateFrom = today.AddDays(-3) });
        Assert.Single(lastThreeDays);
    }

    [Fact]
    public async Task GetAttendanceHistoryAsync_SortsByDateAscendingAndDescending()
    {
        var (_, _, student) = await CreateFixtureAsync();

        var ascending = await Factory.ClassRolls.GetAttendanceHistoryAsync(student.Id, new ClassRollFilter
        {
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Ascending)
        });
        Assert.True(ascending[0].Date < ascending[1].Date);

        var descending = await Factory.ClassRolls.GetAttendanceHistoryAsync(student.Id, new ClassRollFilter
        {
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Descending)
        });
        Assert.True(descending[0].Date > descending[1].Date);
    }

    [Fact]
    public async Task GetAttendanceHistoryAsync_IncludesThePassUsedForEachClass()
    {
        var scheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)scheduleId }).LoadSingle())!;

        var studentId = await Factory.Students.AddAsync(new Student(0, "Alice", "Johnson", null, "alice@example.com", true));
        var student = (await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle())!;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var passId = await Factory.Passes.AddAsync(new CasualPass(0, studentId, 10, new ObservableCollection<PassAlteration>(), 0));
        var pass = await Factory.Passes.GetByIdAsync(passId);

        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-1), schedule, [new ClassRollEntry(student, pass)]));
        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule, [new ClassRollEntry(student, null)]));

        var history = await Factory.ClassRolls.GetAttendanceHistoryAsync(studentId);

        Assert.Equal(2, history.Count);
        Assert.Equal(passId, history.Single(r => r.Date == today.AddDays(-1)).Pass?.Id);
        Assert.Null(history.Single(r => r.Date == today).Pass);
    }

    // The following cover the write path MarkRollViewModel uses to re-save an existing roll
    // (UpdateDetailsAsync + per-student Add/Remove/UpdateStudentEntryPass), plus DeleteAsync. Every
    // assertion reloads through Query, so the SQLite backend is checked against what's actually stored.

    private async Task<(ClassSchedule schedule, Student alice, Student bob, Pass alicePass, Pass otherAlicePass, int rollId)>
        CreateRollFixtureAsync()
    {
        var scheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)scheduleId }).LoadSingle())!;

        var aliceId = await Factory.Students.AddAsync(new Student(0, "Alice", "Johnson", null, "alice@example.com", true));
        var bobId = await Factory.Students.AddAsync(new Student(0, "Bob", "Smith", null, "bob@example.com", true));
        var alice = (await Factory.Students.Query(new StudentFilter { Id = (uint)aliceId }).LoadSingle())!;
        var bob = (await Factory.Students.Query(new StudentFilter { Id = (uint)bobId }).LoadSingle())!;

        var alicePassId = await Factory.Passes.AddAsync(new CasualPass(0, aliceId, 10, new ObservableCollection<PassAlteration>(), 0));
        var otherAlicePassId = await Factory.Passes.AddAsync(new CasualPass(0, aliceId, 5, new ObservableCollection<PassAlteration>(), 0));
        var alicePass = (await Factory.Passes.GetByIdAsync(alicePassId))!;
        var otherAlicePass = (await Factory.Passes.GetByIdAsync(otherAlicePassId))!;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var rollId = await Factory.ClassRolls.AddAsync(new ClassRoll(0, today, schedule, [new ClassRollEntry(alice, alicePass)]));

        return (schedule, alice, bob, alicePass, otherAlicePass, rollId);
    }

    private async Task<ClassRoll?> ReloadRollAsync(int rollId)
    {
        return await Factory.ClassRolls.Query(new ClassRollFilter { Id = (uint)rollId }).LoadSingle();
    }

    [Fact]
    public async Task AddStudentEntryAsync_AddsANewAttendee()
    {
        var (_, alice, bob, alicePass, _, rollId) = await CreateRollFixtureAsync();

        await Factory.ClassRolls.AddStudentEntryAsync(rollId, new ClassRollEntry(bob, null));

        var roll = (await ReloadRollAsync(rollId))!;
        Assert.Equal(2, roll.StudentEntries.Count);
        Assert.Equal(alicePass.Id, roll.StudentEntries.Single(e => e.Student.Id == alice.Id).Pass?.Id);
        Assert.Null(roll.StudentEntries.Single(e => e.Student.Id == bob.Id).Pass);
    }

    /// <summary>A student can only appear once per roll - re-adding them replaces their entry (and
    /// pass) rather than creating a duplicate that would double-charge their pass.</summary>
    [Fact]
    public async Task AddStudentEntryAsync_ReplacesTheExistingEntry_WhenTheStudentIsAlreadyOnTheRoll()
    {
        var (_, alice, _, _, otherAlicePass, rollId) = await CreateRollFixtureAsync();

        await Factory.ClassRolls.AddStudentEntryAsync(rollId, new ClassRollEntry(alice, otherAlicePass));

        var roll = (await ReloadRollAsync(rollId))!;
        var entry = Assert.Single(roll.StudentEntries);
        Assert.Equal(alice.Id, entry.Student.Id);
        Assert.Equal(otherAlicePass.Id, entry.Pass?.Id);
    }

    [Fact]
    public async Task RemoveStudentEntryAsync_RemovesOnlyThatStudent()
    {
        var (_, alice, bob, _, _, rollId) = await CreateRollFixtureAsync();
        await Factory.ClassRolls.AddStudentEntryAsync(rollId, new ClassRollEntry(bob, null));

        await Factory.ClassRolls.RemoveStudentEntryAsync(rollId, alice.Id);

        var roll = (await ReloadRollAsync(rollId))!;
        Assert.Equal(bob.Id, Assert.Single(roll.StudentEntries).Student.Id);
        Assert.Empty(await Factory.ClassRolls.GetAttendanceHistoryAsync(alice.Id));
    }

    [Fact]
    public async Task UpdateStudentEntryPassAsync_SwitchesAndClearsThePass()
    {
        var (_, alice, _, _, otherAlicePass, rollId) = await CreateRollFixtureAsync();

        await Factory.ClassRolls.UpdateStudentEntryPassAsync(rollId, alice.Id, otherAlicePass.Id);
        Assert.Equal(otherAlicePass.Id, Assert.Single((await ReloadRollAsync(rollId))!.StudentEntries).Pass?.Id);

        await Factory.ClassRolls.UpdateStudentEntryPassAsync(rollId, alice.Id, null);
        Assert.Null(Assert.Single((await ReloadRollAsync(rollId))!.StudentEntries).Pass);
    }

    /// <summary>MarkRollViewModel only calls UpdateStudentEntryPassAsync for students already on the
    /// roll; calling it for anyone else must not silently add them.</summary>
    [Fact]
    public async Task UpdateStudentEntryPassAsync_DoesNotAddAStudentWhoIsNotOnTheRoll()
    {
        var (_, _, bob, _, _, rollId) = await CreateRollFixtureAsync();

        await Factory.ClassRolls.UpdateStudentEntryPassAsync(rollId, bob.Id, null);

        var roll = (await ReloadRollAsync(rollId))!;
        Assert.DoesNotContain(roll.StudentEntries, e => e.Student.Id == bob.Id);
    }

    [Fact]
    public async Task UpdateDetailsAsync_ChangesDateAndSchedule_WithoutTouchingAttendees()
    {
        var (_, alice, _, alicePass, _, rollId) = await CreateRollFixtureAsync();
        var otherScheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Thursday, new TimeOnly(18, 30), false));
        var otherSchedule = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)otherScheduleId }).LoadSingle())!;
        var newDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-3);

        // An empty entry list on the passed-in roll must not clear the stored attendees -
        // UpdateDetailsAsync touches only the roll's own columns.
        await Factory.ClassRolls.UpdateDetailsAsync(new ClassRoll(rollId, newDate, otherSchedule, []));

        var roll = (await ReloadRollAsync(rollId))!;
        Assert.Equal(newDate, roll.Date);
        Assert.Equal(otherScheduleId, roll.ClassSchedule.Id);
        var entry = Assert.Single(roll.StudentEntries);
        Assert.Equal(alice.Id, entry.Student.Id);
        Assert.Equal(alicePass.Id, entry.Pass?.Id);
    }

    [Fact]
    public async Task ApplyChangesAsync_AppliesDetailsRemovalsAdditionsAndPassChangesTogether()
    {
        var (schedule, alice, bob, _, otherAlicePass, rollId) = await CreateRollFixtureAsync();
        var carolId = await Factory.Students.AddAsync(new Student(0, "Carol", "Nguyen", null, "carol@example.com", true));
        var carol = (await Factory.Students.Query(new StudentFilter { Id = (uint)carolId }).LoadSingle())!;
        await Factory.ClassRolls.AddStudentEntryAsync(rollId, new ClassRollEntry(bob, null));
        var newDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-2);

        // Remove Bob, switch Alice's pass, add Carol, move the date - one call.
        await Factory.ClassRolls.ApplyChangesAsync(new ClassRoll(rollId, newDate, schedule, []),
            [bob.Id], [new ClassRollEntry(alice, otherAlicePass), new ClassRollEntry(carol, null)]);

        var roll = (await ReloadRollAsync(rollId))!;
        Assert.Equal(newDate, roll.Date);
        Assert.Equal([alice.Id, carolId], roll.StudentEntries.Select(e => e.Student.Id).Order().ToArray());
        Assert.Equal(otherAlicePass.Id, roll.StudentEntries.Single(e => e.Student.Id == alice.Id).Pass?.Id);
        Assert.Null(roll.StudentEntries.Single(e => e.Student.Id == carolId).Pass);
    }

    /// <summary>Attendees the caller didn't mention are left alone - e.g. one added from the Class Rolls
    /// page while the Mark Roll window had this roll open.</summary>
    [Fact]
    public async Task ApplyChangesAsync_LeavesUnmentionedAttendeesAlone()
    {
        var (schedule, alice, bob, alicePass, _, rollId) = await CreateRollFixtureAsync();
        var today = DateOnly.FromDateTime(DateTime.Now);

        await Factory.ClassRolls.ApplyChangesAsync(new ClassRoll(rollId, today, schedule, []), [],
            [new ClassRollEntry(bob, null)]);

        var roll = (await ReloadRollAsync(rollId))!;
        Assert.Equal(2, roll.StudentEntries.Count);
        Assert.Equal(alicePass.Id, roll.StudentEntries.Single(e => e.Student.Id == alice.Id).Pass?.Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheRollAndItsAttendance()
    {
        var (_, alice, _, alicePass, _, rollId) = await CreateRollFixtureAsync();

        await Factory.ClassRolls.DeleteAsync(rollId);

        Assert.Null(await ReloadRollAsync(rollId));
        Assert.Empty(await Factory.ClassRolls.GetAttendanceHistoryAsync(alice.Id));
        Assert.Empty(await Factory.Passes.GetUsageHistoryAsync(alicePass.Id));
    }

    /// <summary>Ties break so each sort still reads chronologically - the Student page's "Classes
    /// attended" sort and the Dashboard's "Recent classes" rely on it. Rolls are inserted so that
    /// primary-key order (the old tiebreak) gives a different answer for every case.</summary>
    [Fact]
    public async Task Query_SortTiesBreakChronologically()
    {
        var morningId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var eveningId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(18, 0), false));
        var morning = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)morningId }).LoadSingle())!;
        var evening = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)eveningId }).LoadSingle())!;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var older = await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-14), morning, []));
        var recentMorning = await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-7), morning, []));
        var recentEvening = await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-7), evening, []));

        async Task<int[]> SortedIds(ClassRollSortOptions key, Order order) =>
            (await Factory.ClassRolls.Query(new ClassRollFilter
            {
                SortBy = new KeyValuePair<ClassRollSortOptions, Order>(key, order)
            }).LoadMultiple()).Select(r => r.Id).ToArray();

        // Date, newest first: the later class on the same day comes first.
        Assert.Equal([recentEvening, recentMorning, older], await SortedIds(ClassRollSortOptions.Date, Order.Descending));
        // Time: same time of day -> most recent date first.
        Assert.Equal([recentMorning, older, recentEvening], await SortedIds(ClassRollSortOptions.Time, Order.Ascending));
        // Day of week (all Monday here): time of day, then most recent date first.
        Assert.Equal([recentMorning, older, recentEvening], await SortedIds(ClassRollSortOptions.DayOfWeek, Order.Ascending));
    }
}
