using YogaClassManager.Core.Data;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests;

public class IdentityRepositoryTests
{
    private static (InMemoryDataStore store, InMemoryIdentityRepository repo) CreateRepo()
    {
        var store = new InMemoryDataStore();
        return (store, new InMemoryIdentityRepository(store));
    }

    [Fact]
    public async Task ArchiveOrDeleteAsync_HardDeletes_WhenIdentityHasNoLinks()
    {
        var (store, repo) = CreateRepo();
        var identity = new Identity(store.NextId(), "Noah", "Robinson", "0400000000", "noah@example.com", true);
        store.People[identity.Id] = identity;

        var result = await repo.ArchiveOrDeleteAsync(identity.Id);

        Assert.Equal(ArchiveResult.Deleted, result);
        Assert.False(store.People.ContainsKey(identity.Id));
    }

    [Fact]
    public async Task ArchiveOrDeleteAsync_SoftArchives_WhenIdentityIsAStudent()
    {
        var (store, repo) = CreateRepo();
        var student = new Student(store.NextId(), "Alice", "Johnson", null, "alice@example.com", true);
        store.People[student.Id] = student;

        var result = await repo.ArchiveOrDeleteAsync(student.Id);

        Assert.Equal(ArchiveResult.Archived, result);
        Assert.True(store.People.ContainsKey(student.Id));
        Assert.False(student.IsActive);
    }

    [Fact]
    public async Task ArchiveOrDeleteAsync_SoftArchives_WhenIdentityIsAnEmergencyContact()
    {
        var (store, repo) = CreateRepo();
        var student = new Student(store.NextId(), "Alice", "Johnson", null, null, true);
        var contact = new Identity(store.NextId(), "Mark", "Johnson", "0400000001", null, true);
        store.People[student.Id] = student;
        store.People[contact.Id] = contact;
        store.EmergencyContactLinks.Add(new EmergencyContactLink(student.Id, contact.Id, Relationship.Parent));

        var result = await repo.ArchiveOrDeleteAsync(contact.Id);

        Assert.Equal(ArchiveResult.Archived, result);
        Assert.True(store.People.ContainsKey(contact.Id));
    }

    [Fact]
    public async Task MergeAsync_Throws_WhenBothIdentitiesAreStudents()
    {
        var (store, repo) = CreateRepo();
        var a = new Student(store.NextId(), "A", "One", null, null, true);
        var b = new Student(store.NextId(), "B", "Two", null, null, true);
        store.People[a.Id] = a;
        store.People[b.Id] = b;

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.MergeAsync(a.Id, b.Id));
    }

    [Fact]
    public async Task MergeAsync_RepointsEmergencyContactLinks_WhenNeitherIsAStudent()
    {
        var (store, repo) = CreateRepo();
        var student = new Student(store.NextId(), "Tom", "Nguyen", null, null, true);
        var survivor = new Identity(store.NextId(), "Jon", "Smith", "0414000001", "jon@example.com", true);
        var duplicate = new Identity(store.NextId(), "Jonathan", "Smith", "0414000001", null, true);
        store.People[student.Id] = student;
        store.People[survivor.Id] = survivor;
        store.People[duplicate.Id] = duplicate;
        store.EmergencyContactLinks.Add(new EmergencyContactLink(student.Id, duplicate.Id, Relationship.Friend));

        var result = await repo.MergeAsync(survivor.Id, duplicate.Id);

        Assert.Equal(1, result.RepointedEmergencyContactLinks);
        Assert.False(store.People.ContainsKey(duplicate.Id));
        Assert.Contains(store.EmergencyContactLinks, l => l.StudentId == student.Id && l.EmergencyContactIdentityId == survivor.Id);
    }

    [Fact]
    public async Task MergeAsync_PromotesSurvivorAndRepointsPassesAndAttendance_WhenDuplicateIsAStudent()
    {
        var (store, repo) = CreateRepo();

        var survivor = new Identity(store.NextId(), "Sara", "OConnor", null, "s.oconnor@example.com", true);
        var duplicateStudent = new Student(store.NextId(), "Sarah", "OConnor", null, "s.oconnor@example.com", true);
        store.People[survivor.Id] = survivor;
        store.People[duplicateStudent.Id] = duplicateStudent;

        var pass = new CasualPass(store.NextId(), duplicateStudent.Id, 5,
            new System.Collections.ObjectModel.ObservableCollection<PassAlteration>(), 1);
        store.Passes[pass.Id] = pass;

        var schedule = new ClassSchedule(store.NextId(), DayOfWeek.Monday, new TimeOnly(9, 0), false);
        var roll = new ClassRoll(store.NextId(), DateOnly.FromDateTime(DateTime.Now), schedule,
            [new ClassRollEntry(duplicateStudent, pass)]);
        store.ClassRolls[roll.Id] = roll;

        var result = await repo.MergeAsync(survivor.Id, duplicateStudent.Id);

        Assert.Equal(1, result.RepointedPasses);
        Assert.Equal(1, result.RepointedAttendanceRecords);
        Assert.False(store.People.ContainsKey(duplicateStudent.Id));
        Assert.IsType<Student>(store.People[survivor.Id]);
        Assert.Equal(survivor.Id, pass.StudentId);
        Assert.Equal(survivor.Id, roll.StudentEntries[0].Student.Id);
    }

    [Fact]
    public async Task Query_FiltersByFirstNameAndIsActive_AndSortsByLastNameDescending()
    {
        var (store, repo) = CreateRepo();
        var a = new Identity(store.NextId(), "Anna", "Zeta", null, null, true);
        var b = new Identity(store.NextId(), "Anna", "Alpha", null, null, true);
        var c = new Identity(store.NextId(), "Anna", "Beta", null, null, false);
        var d = new Identity(store.NextId(), "Bob", "Omega", null, null, true);
        foreach (var p in new[] { a, b, c, d }) store.People[p.Id] = p;

        var filter = new IdentityFilter
        {
            FirstNameFilter = "Anna",
            IsActive = true,
            SortBy = new KeyValuePair<IdentitySortOptions, Order>(IdentitySortOptions.LastName, Order.Descending)
        };

        var results = await repo.Query(filter).LoadMultiple();

        Assert.Equal(["Zeta", "Alpha"], results.Select(p => p.LastName));
    }
}
