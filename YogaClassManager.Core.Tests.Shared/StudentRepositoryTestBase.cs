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
}
