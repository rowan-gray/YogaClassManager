using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Dummy;

/// <summary>
///     A shared in-memory "database" - every InMemory*Repository reads/writes through this single
///     instance so mutations stay consistent across repositories, mirroring a shared SQLite connection.
///
///     People/Students share one identity map (Dictionary&lt;int, Identity&gt;) where Student entries are
///     stored as Student instances - this mirrors the real schema's class-table inheritance (a Student
///     row extends an Identity row) without forking identity between two dictionaries.
/// </summary>
public class InMemoryDataStore
{
    private int nextId = 1;

    public Dictionary<int, Identity> People { get; } = new();
    public List<EmergencyContactLink> EmergencyContactLinks { get; } = new();
    public List<HealthConcernLink> HealthConcernLinks { get; } = new();
    public Dictionary<int, Pass> Passes { get; } = new();
    public Dictionary<int, PassAlteration> Alterations { get; } = new();
    public Dictionary<int, ClassSchedule> ClassSchedules { get; } = new();
    public Dictionary<int, ClassRoll> ClassRolls { get; } = new();
    public Dictionary<int, Term> Terms { get; } = new();

    public int NextId()
    {
        return nextId++;
    }

    /// <summary>Clears every collection and resets the id counter, ready for DummyDataSeeder.Seed to run again.</summary>
    public void Reset()
    {
        People.Clear();
        EmergencyContactLinks.Clear();
        HealthConcernLinks.Clear();
        Passes.Clear();
        Alterations.Clear();
        ClassSchedules.Clear();
        ClassRolls.Clear();
        Terms.Clear();
        nextId = 1;
    }
}

public record EmergencyContactLink(int StudentId, int EmergencyContactIdentityId, Relationship Relationship);

public record HealthConcernLink(int StudentId, string Concern);
