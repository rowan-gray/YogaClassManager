using System.Collections.ObjectModel;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests.Shared;

public abstract class IdentityRepositoryTestBase<TFactory> : IAsyncLifetime
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

    [Fact]
    public async Task ArchiveOrDeleteAsync_HardDeletes_WhenIdentityHasNoLinks()
    {
        var identityId = await Factory.Identities.AddAsync(
            new Identity(0, "Noah", "Robinson", "0400000000", "noah@example.com", true));

        var result = await Factory.Identities.ArchiveOrDeleteAsync(identityId);

        Assert.Equal(ArchiveResult.Deleted, result);
        Assert.Null(await Factory.Identities.Query(new IdentityFilter { Id = (uint)identityId }).LoadSingle());
    }

    [Fact]
    public async Task ArchiveOrDeleteAsync_SoftArchives_WhenIdentityIsAStudent()
    {
        var studentId = await Factory.Students.AddAsync(
            new Student(0, "Alice", "Johnson", null, "alice@example.com", true));

        var result = await Factory.Identities.ArchiveOrDeleteAsync(studentId);

        Assert.Equal(ArchiveResult.Archived, result);
        var student = await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle();
        Assert.NotNull(student);
        Assert.False(student!.IsActive);
    }

    [Fact]
    public async Task ArchiveOrDeleteAsync_SoftArchives_WhenIdentityIsAnEmergencyContact()
    {
        var studentId = await Factory.Students.AddAsync(
            new Student(0, "Alice", "Johnson", null, "alice@example.com", true));
        var contactId = await Factory.Identities.AddAsync(
            new Identity(0, "Mark", "Johnson", "0400000001", null, true));

        await Factory.Students.LinkEmergencyContactAsync(studentId, contactId, Relationship.Parent);

        var result = await Factory.Identities.ArchiveOrDeleteAsync(contactId);

        Assert.Equal(ArchiveResult.Archived, result);
        Assert.NotNull(await Factory.Identities.Query(new IdentityFilter { Id = (uint)contactId }).LoadSingle());
    }

    [Fact]
    public async Task MergeAsync_Throws_WhenBothIdentitiesAreStudents()
    {
        var aId = await Factory.Students.AddAsync(new Student(0, "A", "One", "0400000010", null, true));
        var bId = await Factory.Students.AddAsync(new Student(0, "B", "Two", "0400000011", null, true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Factory.Identities.MergeAsync(aId, bId));
    }

    [Fact]
    public async Task MergeAsync_RepointsEmergencyContactLinks_WhenNeitherIsAStudent()
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Tom", "Nguyen", "0400000020", null, true));
        var survivorId = await Factory.Identities.AddAsync(
            new Identity(0, "Jon", "Smith", "0414000001", "jon@example.com", true));
        var duplicateId = await Factory.Identities.AddAsync(
            new Identity(0, "Jonathan", "Smith", "0414000001", null, true));

        await Factory.Students.LinkEmergencyContactAsync(studentId, duplicateId, Relationship.Friend);

        var result = await Factory.Identities.MergeAsync(survivorId, duplicateId);

        Assert.Equal(1, result.RepointedEmergencyContactLinks);
        Assert.Null(await Factory.Identities.Query(new IdentityFilter { Id = (uint)duplicateId }).LoadSingle());

        var contacts = await Factory.Students.GetEmergencyContactsAsync(studentId);
        Assert.Contains(contacts, c => c.Id == survivorId);
    }

    [Fact]
    public async Task MergeAsync_PromotesSurvivorAndRepointsPassesAndAttendance_WhenDuplicateIsAStudent()
    {
        var survivorId = await Factory.Identities.AddAsync(
            new Identity(0, "Sara", "OConnor", null, "s.oconnor@example.com", true));
        var duplicateStudentId = await Factory.Students.AddAsync(
            new Student(0, "Sarah", "OConnor", "0400000030", null, true));

        var pass = new CasualPass(0, duplicateStudentId, 5, new ObservableCollection<PassAlteration>(), 1);
        var passId = await Factory.Passes.AddAsync(pass);

        var scheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)scheduleId }).LoadSingle())!;
        var duplicateStudent = (await Factory.Students.Query(new StudentFilter { Id = (uint)duplicateStudentId }).LoadSingle())!;
        var loadedPass = await Factory.Passes.GetByIdAsync(passId);

        var rollId = await Factory.ClassRolls.AddAsync(new ClassRoll(0, DateOnly.FromDateTime(DateTime.Now), schedule,
            [new ClassRollEntry(duplicateStudent, loadedPass)]));

        var result = await Factory.Identities.MergeAsync(survivorId, duplicateStudentId);

        Assert.Equal(1, result.RepointedPasses);
        Assert.Equal(1, result.RepointedAttendanceRecords);
        Assert.Null(await Factory.Identities.Query(new IdentityFilter { Id = (uint)duplicateStudentId }).LoadSingle());

        var survivorAsStudent = await Factory.Students.Query(new StudentFilter { Id = (uint)survivorId }).LoadSingle();
        Assert.NotNull(survivorAsStudent);

        var reloadedPass = await Factory.Passes.GetByIdAsync(passId);
        Assert.Equal(survivorId, reloadedPass!.StudentId);

        var history = await Factory.ClassRolls.GetAttendanceHistoryAsync(survivorId);
        Assert.Contains(history, r => r.ClassRollId == rollId);
    }

    [Fact]
    public async Task Query_FiltersByFirstNameAndIsActive_AndSortsByLastNameDescending()
    {
        var aId = await Factory.Identities.AddAsync(new Identity(0, "Anna", "Zeta", "0400000040", null, true));
        var bId = await Factory.Identities.AddAsync(new Identity(0, "Anna", "Alpha", "0400000041", null, true));
        await Factory.Identities.AddAsync(new Identity(0, "Anna", "Beta", "0400000042", null, false));
        await Factory.Identities.AddAsync(new Identity(0, "Bob", "Omega", "0400000043", null, true));

        var filter = new IdentityFilter
        {
            FirstNameFilter = "Anna",
            IsActive = true,
            SortBy = new KeyValuePair<IdentitySortOptions, Order>(IdentitySortOptions.LastName, Order.Descending)
        };

        var results = await Factory.Identities.Query(filter).LoadMultiple();

        Assert.Equal(["Zeta", "Alpha"], results.Select(p => p.LastName));
        Assert.Equal([aId, bId], results.Select(p => p.Id));
    }
}
