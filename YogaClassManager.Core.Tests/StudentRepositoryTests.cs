using System.Collections.ObjectModel;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests;

public class StudentRepositoryTests
{
    private static (InMemoryDataStore store, InMemoryStudentRepository repo) CreateRepo()
    {
        var store = new InMemoryDataStore();
        var identityRepo = new InMemoryIdentityRepository(store);
        var passRepo = new InMemoryPassRepository(store);
        var emergencyContactRepo = new InMemoryEmergencyContactRepository(store);
        return (store, new InMemoryStudentRepository(store, identityRepo, passRepo, emergencyContactRepo));
    }

    [Fact]
    public async Task LinkAndUnlinkEmergencyContact_RoundTrips()
    {
        var (store, repo) = CreateRepo();
        var student = new Student(store.NextId(), "Ben", "Carter", null, null, true);
        var contact = new Identity(store.NextId(), "Priya", "Patel", null, null, true);
        store.People[student.Id] = student;
        store.People[contact.Id] = contact;

        await repo.LinkEmergencyContactAsync(student.Id, contact.Id, Relationship.Friend);
        var contacts = await repo.GetEmergencyContactsAsync(student.Id);
        Assert.Single(contacts);
        Assert.Equal(Relationship.Friend, contacts[0].Relationship);

        await repo.UnlinkEmergencyContactAsync(student.Id, contact.Id);
        contacts = await repo.GetEmergencyContactsAsync(student.Id);
        Assert.Empty(contacts);
    }

    [Fact]
    public async Task HealthConcerns_CanBeAddedAndRemoved()
    {
        var (store, repo) = CreateRepo();
        var student = new Student(store.NextId(), "Daniel", "Evans", null, null, true);
        store.People[student.Id] = student;

        await repo.AddHealthConcernAsync(student.Id, "Asthma");
        await repo.AddHealthConcernAsync(student.Id, "Asthma"); // duplicate add should not double up
        Assert.Equal(["Asthma"], await repo.GetHealthConcernsAsync(student.Id));

        await repo.RemoveHealthConcernAsync(student.Id, "Asthma");
        Assert.Empty(await repo.GetHealthConcernsAsync(student.Id));
    }

    [Fact]
    public async Task GetPassesAsync_ExcludesExpiredAndDepleted_UnlessRequested()
    {
        var (store, repo) = CreateRepo();
        var student = new Student(store.NextId(), "Grace", "Kim", null, null, true);
        store.People[student.Id] = student;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var active = new CasualPass(store.NextId(), student.Id, 5, new ObservableCollection<PassAlteration>(), 1);
        var depleted = new CasualPass(store.NextId(), student.Id, 3, new ObservableCollection<PassAlteration>(), 3);
        var expired = new DatedPass(store.NextId(), student.Id, 5, new ObservableCollection<PassAlteration>(), 1,
            today.AddDays(-30), today.AddDays(-10));
        foreach (var p in new Pass[] { active, depleted, expired }) store.Passes[p.Id] = p;

        var defaultResult = await repo.GetPassesAsync(student.Id);
        Assert.Single(defaultResult);
        Assert.Same(active, defaultResult[0]);

        var withExpiredAndDepleted = await repo.GetPassesAsync(student.Id, includeExpired: true, includeDepleted: true);
        Assert.Equal(3, withExpiredAndDepleted.Count);
    }

    [Fact]
    public async Task PromoteToStudentAsync_ConvertsIdentityInPlace_PreservingIdAndLinks()
    {
        var (store, repo) = CreateRepo();
        var identity = new Identity(store.NextId(), "Nora", "Fox", null, "nora@example.com", true);
        store.People[identity.Id] = identity;

        var other = new Student(store.NextId(), "Sam", "Reid", null, null, true);
        store.People[other.Id] = other;
        store.EmergencyContactLinks.Add(new EmergencyContactLink(other.Id, identity.Id, Relationship.Friend));

        var promoted = new Student(identity.Id, "Nora", "Fox", null, "nora@example.com", true);
        var resultId = await repo.PromoteToStudentAsync(promoted);

        Assert.Equal(identity.Id, resultId);
        Assert.IsType<Student>(store.People[identity.Id]);
        Assert.Contains(store.EmergencyContactLinks, l => l.EmergencyContactIdentityId == identity.Id);
    }

    [Fact]
    public async Task PromoteToStudentAsync_Throws_WhenAlreadyAStudent()
    {
        var (store, repo) = CreateRepo();
        var student = new Student(store.NextId(), "Alex", "Young", null, null, true);
        store.People[student.Id] = student;

        var promoted = new Student(student.Id, "Alex", "Young", null, null, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.PromoteToStudentAsync(promoted));
    }

    [Fact]
    public async Task PromoteToStudentAsync_Throws_WhenIdentityDoesNotExist()
    {
        var (_, repo) = CreateRepo();
        var promoted = new Student(999, "Ghost", "Nobody", null, null, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.PromoteToStudentAsync(promoted));
    }

    [Fact]
    public async Task Query_AttachesLiveChildRepositoriesToReturnedStudent()
    {
        var (store, repo) = CreateRepo();
        var student = new Student(store.NextId(), "Isla", "Moore", null, null, true);
        store.People[student.Id] = student;

        var contact = new Identity(store.NextId(), "Liam", "Walsh", null, null, true);
        store.People[contact.Id] = contact;
        await repo.LinkEmergencyContactAsync(student.Id, contact.Id, Relationship.Friend);

        var pass = new CasualPass(store.NextId(), student.Id, 5, new ObservableCollection<PassAlteration>(), 1);
        store.Passes[pass.Id] = pass;

        var loaded = await repo.Query(new StudentFilter { Id = (uint)student.Id }).LoadSingle();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.Passes);
        Assert.NotNull(loaded.EmergencyContacts);

        var loadedPasses = await loaded.Passes!.LoadMultiple();
        var loadedContacts = await loaded.EmergencyContacts!.LoadMultiple();

        Assert.Same(pass, Assert.Single(loadedPasses));
        Assert.Equal(contact.Id, Assert.Single(loadedContacts).Id);
    }
}
