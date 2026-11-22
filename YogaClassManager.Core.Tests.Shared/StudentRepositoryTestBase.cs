using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Data;
using System.Collections.ObjectModel;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests.Shared;

public abstract class StudentRepositoryTestBase<TFactory> : IAsyncLifetime
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
    public async Task LinkAndUnlinkEmergencyContact_RoundTrips()
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Ben", "Carter", null, "ben@example.com", true));
        var contactId = await Factory.Identities.AddAsync(new Identity(0, "Priya", "Patel", null, "priya@example.com", true));

        await Factory.Students.LinkEmergencyContactAsync(studentId, contactId, Relationship.Friend);
        var contacts = await Factory.Students.GetEmergencyContactsAsync(studentId);
        Assert.Single(contacts);
        Assert.Equal(Relationship.Friend, contacts[0].Relationship);

        await Factory.Students.UnlinkEmergencyContactAsync(studentId, contactId);
        contacts = await Factory.Students.GetEmergencyContactsAsync(studentId);
        Assert.Empty(contacts);
    }

    [Fact]
    public async Task HealthConcerns_CanBeAddedAndRemoved()
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Daniel", "Evans", null, "daniel@example.com", true));

        await Factory.Students.AddHealthConcernAsync(studentId, "Asthma");
        await Factory.Students.AddHealthConcernAsync(studentId, "Asthma"); // duplicate add should not double up
        Assert.Equal(["Asthma"], await Factory.Students.GetHealthConcernsAsync(studentId));

        await Factory.Students.RemoveHealthConcernAsync(studentId, "Asthma");
        Assert.Empty(await Factory.Students.GetHealthConcernsAsync(studentId));
    }

    /// <summary>Saves through a fresh Student object (as the edit dialog does), never the loaded one -
    /// the in-memory backend would otherwise pass trivially by reference.</summary>
    [Fact]
    public async Task UpdateAsync_PersistsEditedDetails_AndKeepsPassesAndHealthConcerns()
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Grace", "Kim", "0400000000", "grace@example.com", true));
        await Factory.Students.AddHealthConcernAsync(studentId, "Knee injury");
        var passId = await Factory.Passes.AddAsync(new CasualPass(0, studentId, 5, new ObservableCollection<PassAlteration>(), 0));

        await Factory.Students.UpdateAsync(new Student(studentId, "Gracie", "Kim-Lee", null, "gracie@example.com", true));

        var reloaded = (await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle())!;
        Assert.Equal(("Gracie", "Kim-Lee", (string?)null, "gracie@example.com"),
            (reloaded.FirstName, reloaded.LastName, reloaded.PhoneNumber, reloaded.Email));
        Assert.Equal(["Knee injury"], await Factory.Students.GetHealthConcernsAsync(studentId));
        Assert.Equal(passId, Assert.Single(await Factory.Students.GetPassesAsync(studentId)).Id);
    }

    [Fact]
    public async Task UpdateAsync_ChangesOnlyTheGivenStudent()
    {
        var graceId = await Factory.Students.AddAsync(new Student(0, "Grace", "Kim", null, "grace@example.com", true));
        var henryId = await Factory.Students.AddAsync(new Student(0, "Henry", "Park", null, "henry@example.com", true));

        await Factory.Students.UpdateAsync(new Student(graceId, "Gracie", "Kim", null, "grace@example.com", true));

        var henry = (await Factory.Students.Query(new StudentFilter { Id = (uint)henryId }).LoadSingle())!;
        Assert.Equal("Henry", henry.FirstName);
    }

    [Fact]
    public async Task GetPassesAsync_ExcludesExpiredAndDepleted_UnlessRequested()
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Grace", "Kim", null, "grace@example.com", true));

        var today = DateOnly.FromDateTime(DateTime.Now);
        var activeId = await Factory.Passes.AddAsync(new CasualPass(0, studentId, 5, new ObservableCollection<PassAlteration>(), 0));

        var depletedId = await Factory.Passes.AddAsync(new CasualPass(0, studentId, 3, new ObservableCollection<PassAlteration>(), 0));
        // ClassesUsed is a derived value (from real class-roll attendance), not a stored/settable
        // field - simulate "already used 3 of 3" (depleted) via the seed hook.
        await Factory.SeedPassUsageAsync(depletedId, studentId, usesCount: 3);

        await Factory.Passes.AddAsync(new DatedPass(0, studentId, 5, new ObservableCollection<PassAlteration>(), 0,
            today.AddDays(-30), today.AddDays(-10))); // expired

        var defaultResult = await Factory.Students.GetPassesAsync(studentId);
        Assert.Single(defaultResult);
        Assert.Equal(activeId, defaultResult[0].Id);

        var withExpiredAndDepleted = await Factory.Students.GetPassesAsync(studentId, includeExpired: true, includeDepleted: true);
        Assert.Equal(3, withExpiredAndDepleted.Count);
    }

    [Fact]
    public async Task PromoteToStudentAsync_ConvertsIdentityInPlace_PreservingIdAndLinks()
    {
        var identityId = await Factory.Identities.AddAsync(new Identity(0, "Nora", "Fox", null, "nora@example.com", true));

        var otherStudentId = await Factory.Students.AddAsync(new Student(0, "Sam", "Reid", "0400000050", null, true));
        await Factory.Students.LinkEmergencyContactAsync(otherStudentId, identityId, Relationship.Friend);

        var promoted = new Student(identityId, "Nora", "Fox", null, "nora@example.com", true);
        var resultId = await Factory.Students.PromoteToStudentAsync(promoted);

        Assert.Equal(identityId, resultId);
        Assert.NotNull(await Factory.Students.Query(new StudentFilter { Id = (uint)identityId }).LoadSingle());

        var contacts = await Factory.Students.GetEmergencyContactsAsync(otherStudentId);
        Assert.Contains(contacts, c => c.Id == identityId);
    }

    [Fact]
    public async Task PromoteToStudentAsync_Throws_WhenAlreadyAStudent()
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Alex", "Young", "0400000051", null, true));

        var promoted = new Student(studentId, "Alex", "Young", "0400000051", null, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Factory.Students.PromoteToStudentAsync(promoted));
    }

    [Fact]
    public async Task PromoteToStudentAsync_Throws_WhenIdentityDoesNotExist()
    {
        var promoted = new Student(999999, "Ghost", "Nobody", "0400000052", null, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Factory.Students.PromoteToStudentAsync(promoted));
    }

    [Fact]
    public async Task Query_AttachesLiveChildRepositoriesToReturnedStudent()
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Isla", "Moore", null, "isla@example.com", true));

        var contactId = await Factory.Identities.AddAsync(new Identity(0, "Liam", "Walsh", null, "liam@example.com", true));
        await Factory.Students.LinkEmergencyContactAsync(studentId, contactId, Relationship.Friend);

        var passId = await Factory.Passes.AddAsync(new CasualPass(0, studentId, 5, new ObservableCollection<PassAlteration>(), 1));

        var loaded = await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.Passes);
        Assert.NotNull(loaded.EmergencyContacts);

        var loadedPasses = await loaded.Passes!.LoadMultiple();
        var loadedContacts = await loaded.EmergencyContacts!.LoadMultiple();

        Assert.Equal(passId, Assert.Single(loadedPasses).Id);
        Assert.Equal(contactId, Assert.Single(loadedContacts).Id);
    }

    /// <summary>Every sort key, both directions; students who've never attended sort last whichever
    /// way Last attendance is sorted.</summary>
    [Theory]
    [InlineData(StudentSortOptions.Id, Order.Ascending)]
    [InlineData(StudentSortOptions.Id, Order.Descending)]
    [InlineData(StudentSortOptions.FirstName, Order.Ascending)]
    [InlineData(StudentSortOptions.FirstName, Order.Descending)]
    [InlineData(StudentSortOptions.LastName, Order.Ascending)]
    [InlineData(StudentSortOptions.LastName, Order.Descending)]
    [InlineData(StudentSortOptions.LastAttendance, Order.Ascending)]
    [InlineData(StudentSortOptions.LastAttendance, Order.Descending)]
    public async Task Query_SortsByEveryKeyInBothDirections(StudentSortOptions key, Order order)
    {
        int[] ids =
        [
            await Factory.Students.AddAsync(new Student(0, "Dana", "Adams", null, "dana@example.com", true)),
            await Factory.Students.AddAsync(new Student(0, "Bea", "Clark", null, "bea@example.com", true)),
            await Factory.Students.AddAsync(new Student(0, "Cara", "Brown", null, "cara@example.com", true)),
            await Factory.Students.AddAsync(new Student(0, "Amy", "Drew", null, "amy@example.com", true))
        ];

        // Last attended: Bea 14 days ago, Dana 7, Amy 1; Cara never.
        var scheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)scheduleId }).LoadSingle())!;
        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var (index, daysAgo) in new[] { (1, 14), (0, 7), (3, 1) })
        {
            var student = (await Factory.Students.Query(new StudentFilter { Id = (uint)ids[index] }).LoadSingle())!;
            await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-daysAgo), schedule, [new ClassRollEntry(student, null)]));
        }

        int[] expected = (key, order) switch
        {
            (StudentSortOptions.FirstName, _) => [3, 1, 2, 0],
            (StudentSortOptions.LastName, _) => [0, 2, 1, 3],
            (StudentSortOptions.LastAttendance, Order.Ascending) => [1, 0, 3, 2],
            (StudentSortOptions.LastAttendance, _) => [3, 0, 1, 2],
            _ => [0, 1, 2, 3]
        };
        if (order == Order.Descending && key != StudentSortOptions.LastAttendance)
            Array.Reverse(expected);

        var results = await Factory.Students.Query(new StudentFilter
        {
            SortBy = new KeyValuePair<StudentSortOptions, Order>(key, order)
        }).LoadMultiple();

        Assert.Equal(expected.Select(i => ids[i]), results.Select(s => s.Id));
    }

    [Theory]
    [InlineData(EmergencyContactSortOptions.Id, Order.Ascending)]
    [InlineData(EmergencyContactSortOptions.Id, Order.Descending)]
    [InlineData(EmergencyContactSortOptions.FirstName, Order.Ascending)]
    [InlineData(EmergencyContactSortOptions.FirstName, Order.Descending)]
    [InlineData(EmergencyContactSortOptions.LastName, Order.Ascending)]
    [InlineData(EmergencyContactSortOptions.LastName, Order.Descending)]
    public async Task EmergencyContactsQuery_SortsByEveryKeyInBothDirections(EmergencyContactSortOptions key, Order order)
    {
        var studentId = await Factory.Students.AddAsync(new Student(0, "Test", "Student", null, "test@example.com", true));
        int[] ids =
        [
            await Factory.Identities.AddAsync(new Identity(0, "Dana", "Adams", "0400 000 001", null, true)),
            await Factory.Identities.AddAsync(new Identity(0, "Bea", null, "0400 000 002", null, true)),
            await Factory.Identities.AddAsync(new Identity(0, "Cara", "Brown", "0400 000 003", null, true))
        ];
        foreach (var id in ids)
            await Factory.Students.LinkEmergencyContactAsync(studentId, id, Relationship.Parent);

        int[] ascending = key switch
        {
            EmergencyContactSortOptions.FirstName => [1, 2, 0],
            EmergencyContactSortOptions.LastName => [1, 0, 2],
            _ => [0, 1, 2]
        };

        var results = await Factory.EmergencyContacts.Query(new EmergencyContactFilter
        {
            StudentId = studentId,
            SortBy = new KeyValuePair<EmergencyContactSortOptions, Order>(key, order)
        }).LoadMultiple();

        var expected = ascending.Select(i => ids[i]);
        Assert.Equal(order == Order.Ascending ? expected : expected.Reverse(), results.Select(c => c.Id));
    }
}
