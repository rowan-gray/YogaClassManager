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
    public async Task UpdateAsync_PersistsEditedDetails_IncludingClearedOptionalFields()
    {
        var identityId = await Factory.Identities.AddAsync(
            new Identity(0, "Noah", "Robinson", "0400000000", "noah@example.com", true));

        // A fresh object, as the edit dialog saves - not the instance the repository handed out.
        await Factory.Identities.UpdateAsync(new Identity(identityId, "Noel", null, "0411111111", null, true));

        var reloaded = (await Factory.Identities.Query(new IdentityFilter { Id = (uint)identityId }).LoadSingle())!;
        Assert.Equal(("Noel", (string?)null, "0411111111", (string?)null),
            (reloaded.FirstName, reloaded.LastName, reloaded.PhoneNumber, reloaded.Email));
    }

    [Fact]
    public async Task UpdateAsync_KeepsAnIdentitysStudentStatus()
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Ava", "Lee", null, "ava@example.com", true));

        await Factory.Identities.UpdateAsync(new Identity(studentId, "Ava", "Lee-Smith", null, "ava@example.com", true));

        var student = (await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle())!;
        Assert.Equal("Lee-Smith", student.LastName);
        Assert.True((await Factory.Identities.GetLinkageSummaryAsync(studentId)).IsStudent);
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

    /// <summary>Every sort key, both directions. Each key orders the four records differently (and
    /// differently from insertion order), and a missing value sorts first ascending / last descending.</summary>
    [Theory]
    [InlineData(IdentitySortOptions.Id, Order.Ascending)]
    [InlineData(IdentitySortOptions.Id, Order.Descending)]
    [InlineData(IdentitySortOptions.FirstName, Order.Ascending)]
    [InlineData(IdentitySortOptions.FirstName, Order.Descending)]
    [InlineData(IdentitySortOptions.LastName, Order.Ascending)]
    [InlineData(IdentitySortOptions.LastName, Order.Descending)]
    [InlineData(IdentitySortOptions.PhoneNumber, Order.Ascending)]
    [InlineData(IdentitySortOptions.PhoneNumber, Order.Descending)]
    [InlineData(IdentitySortOptions.Email, Order.Ascending)]
    [InlineData(IdentitySortOptions.Email, Order.Descending)]
    public async Task Query_SortsByEveryKeyInBothDirections(IdentitySortOptions key, Order order)
    {
        int[] ids =
        [
            await Factory.Identities.AddAsync(new Identity(0, "Dana", "Adams", "0400 000 003", "b@example.com", true)),
            await Factory.Identities.AddAsync(new Identity(0, "Bea", "Clark", null, "d@example.com", true)),
            await Factory.Identities.AddAsync(new Identity(0, "Cara", null, "0400 000 001", null, true)),
            await Factory.Identities.AddAsync(new Identity(0, "Amy", "Brown", "0400 000 002", "a@example.com", true))
        ];
        int[] ascending = key switch
        {
            IdentitySortOptions.FirstName => [3, 1, 2, 0],
            IdentitySortOptions.LastName => [2, 0, 3, 1],
            IdentitySortOptions.PhoneNumber => [1, 2, 3, 0],
            IdentitySortOptions.Email => [2, 3, 0, 1],
            _ => [0, 1, 2, 3]
        };

        var results = await Factory.Identities.Query(new IdentityFilter
        {
            SortBy = new KeyValuePair<IdentitySortOptions, Order>(key, order)
        }).LoadMultiple();

        var expected = ascending.Select(i => ids[i]);
        Assert.Equal(order == Order.Ascending ? expected : expected.Reverse(), results.Select(p => p.Id));
    }

    [Theory]
    [InlineData(Order.Ascending)]
    [InlineData(Order.Descending)]
    public async Task Query_SortTiesBreakByIdAscending_InEitherDirection(Order order)
    {
        var first = await Factory.Identities.AddAsync(new Identity(0, "Sam", "Lee", null, "sam.lee@example.com", true));
        var second = await Factory.Identities.AddAsync(new Identity(0, "Sam", "Ng", null, "sam.ng@example.com", true));

        var results = await Factory.Identities.Query(new IdentityFilter
        {
            SortBy = new KeyValuePair<IdentitySortOptions, Order>(IdentitySortOptions.FirstName, order)
        }).LoadMultiple();

        Assert.Equal([first, second], results.Select(p => p.Id));
    }

    /// <summary>Surnames like "van Dyke" sort among the V/W names, not after every capitalised one -
    /// i.e. both stores compare names case-insensitively.</summary>
    [Fact]
    public async Task Query_SortByName_IgnoresCase()
    {
        var wilson = await Factory.Identities.AddAsync(new Identity(0, "Kim", "Wilson", null, "kim@example.com", true));
        var vanDyke = await Factory.Identities.AddAsync(new Identity(0, "Jo", "van Dyke", null, "jo@example.com", true));
        var adams = await Factory.Identities.AddAsync(new Identity(0, "Al", "Adams", null, "al@example.com", true));

        var results = await Factory.Identities.Query(new IdentityFilter
        {
            SortBy = new KeyValuePair<IdentitySortOptions, Order>(IdentitySortOptions.LastName, Order.Ascending)
        }).LoadMultiple();

        Assert.Equal([adams, vanDyke, wilson], results.Select(p => p.Id));
    }
}
