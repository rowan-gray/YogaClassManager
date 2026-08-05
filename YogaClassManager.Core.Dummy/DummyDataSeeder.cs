using System.Collections.ObjectModel;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Dummy;

/// <summary>
///     Populates an InMemoryDataStore with enough realistic sample data to exercise every requirement:
///     duplicate people for merge testing, students with varying links, every pass type/state,
///     archived classes, a completed term, and weeks of attendance history.
/// </summary>
public static class DummyDataSeeder
{
    public static void Seed(InMemoryDataStore store)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        // --- Class schedules (7, one archived) -----------------------------------------------
        var monMorning = AddSchedule(store, DayOfWeek.Monday, new TimeOnly(9, 0));
        var monEvening = AddSchedule(store, DayOfWeek.Monday, new TimeOnly(18, 0));
        var tueMorning = AddSchedule(store, DayOfWeek.Tuesday, new TimeOnly(10, 0));
        var wedMorning = AddSchedule(store, DayOfWeek.Wednesday, new TimeOnly(9, 0));
        var thuEvening = AddSchedule(store, DayOfWeek.Thursday, new TimeOnly(18, 0));
        var friMorning = AddSchedule(store, DayOfWeek.Friday, new TimeOnly(9, 0));
        var satMorning = AddSchedule(store, DayOfWeek.Saturday, new TimeOnly(10, 0), isArchived: true);

        // --- Terms (one active, one completed) ------------------------------------------------
        var activeTerm = AddTerm(store, "Term 3 2026", today.AddDays(-14), today.AddDays(70), null, null);
        var monTermClass = new TermClassSchedule(monMorning, 10, 0);
        var friTermClass = new TermClassSchedule(friMorning, 10, 0);
        activeTerm.Classes.Add(monTermClass);
        activeTerm.Classes.Add(friTermClass);

        var completedTerm = AddTerm(store, "Term 2 2026", today.AddDays(-120), today.AddDays(-30),
            today.AddDays(-35), today.AddDays(-20));
        var tueTermClass = new TermClassSchedule(tueMorning, 8, 0);
        completedTerm.Classes.Add(tueTermClass);

        // --- Identities / Students -----------------------------------------------------------------
        var alice = AddStudent(store, "Alice", "Johnson", "0412000001", "alice.johnson@example.com");
        alice.HealthConcerns.Add("Asthma");

        var ben = AddStudent(store, "Ben", "Carter", "0412000002", "ben.carter@example.com");
        var chloe = AddStudent(store, "Chloe", "Davis", "0412000003", "chloe.davis@example.com");
        var daniel = AddStudent(store, "Daniel", "Evans", "0412000004", "daniel.evans@example.com");
        daniel.HealthConcerns.Add("Bad knee");
        daniel.HealthConcerns.Add("Peanut allergy");

        var emma = AddStudent(store, "Emma", "Foster", "0412000005", "emma.foster@example.com");
        var sarahOConnor = AddStudent(store, "Sarah", "OConnor", "0412000006", "s.oconnor@example.com");
        var tom = AddStudent(store, "Tom", "Nguyen", "0412000007", "tom.nguyen@example.com");
        var grace = AddStudent(store, "Grace", "Kim", "0412000008", "grace.kim@example.com");
        var henry = AddStudent(store, "Henry", "Lee", "0412000009", "henry.lee@example.com");
        var isla = AddStudent(store, "Isla", "Moore", "0412000010", "isla.moore@example.com");

        // Plain (non-student) people, some linked as emergency contacts, some fully unlinked.
        var mark = AddIdentity(store, "Mark", "Johnson", "0413000001", "mark.johnson@example.com");
        var priya = AddIdentity(store, "Priya", "Patel", "0413000002", "priya.patel@example.com");
        var liam = AddIdentity(store, "Liam", "Walsh", "0413000003", "liam.walsh@example.com");
        var olivia = AddIdentity(store, "Olivia", "Chen", "0413000004", "olivia.chen@example.com");

        // Duplicate pair 1: neither is a Student, no links at all - the simple merge case.
        var jonSmith = AddIdentity(store, "Jon", "Smith", "0414000001", "jon.smith@example.com");
        var jonathanSmith = AddIdentity(store, "Jonathan", "Smith", "0414000001", null);

        // Duplicate pair 2: the duplicate is only someone else's emergency contact (Tom's) - exercises
        // the repoint-and-promote merge path when the *surviving* record is a Student.
        var saraOConnorDuplicate = AddIdentity(store, "Sara", "O'Connor", null, "s.oconnor@example.com");

        // Fully unlinked person, for the plain hard-delete-on-archive scenario.
        var noah = AddIdentity(store, "Noah", "Robinson", "0415000000", "noah.robinson@example.com");

        // --- Emergency contacts ------------------------------------------------------------------
        LinkContact(store, alice, mark, Relationship.Parent);
        LinkContact(store, ben, priya, Relationship.Friend);
        LinkContact(store, ben, alice, Relationship.Parent); // Alice: a Student who is also someone else's contact.
        LinkContact(store, chloe, liam, Relationship.Partner);
        LinkContact(store, daniel, olivia, Relationship.Spouse);
        LinkContact(store, daniel, mark, Relationship.Other);
        LinkContact(store, tom, saraOConnorDuplicate, Relationship.Friend);
        LinkContact(store, tom, priya, Relationship.Other);
        LinkContact(store, grace, olivia, Relationship.Friend);
        LinkContact(store, isla, liam, Relationship.Friend);

        // --- Passes: every type, every expiry/depletion state -----------------------------------
        var aliceActiveDated = AddPass(store, new DatedPass(0, alice.Id, 10, new ObservableCollection<PassAlteration>(),
            3, today.AddDays(-10), today.AddDays(20)));
        AddAlteration(store, aliceActiveDated, 2, "Makeup class credit");

        AddPass(store, new DatedPass(0, alice.Id, 5, new ObservableCollection<PassAlteration>(), 5,
            today.AddDays(-60), today.AddDays(-30))); // expired

        var benActiveCasual = AddPass(store, new CasualPass(0, ben.Id, 5, new ObservableCollection<PassAlteration>(), 2));
        AddPass(store, new CasualPass(0, ben.Id, 3, new ObservableCollection<PassAlteration>(), 3)); // depleted

        var chloeActiveTerm = AddPass(store, new TermPass(0, chloe.Id, 4, new ObservableCollection<PassAlteration>(),
            activeTerm, friTermClass));

        var danielExpiredTerm = AddPass(store, new TermPass(0, daniel.Id, 8,
            new ObservableCollection<PassAlteration>(), completedTerm, tueTermClass)); // expired (term completed)

        var gracePassiveCasual =
            AddPass(store, new CasualPass(0, grace.Id, 12, new ObservableCollection<PassAlteration>(), 4));

        var henryActiveDated = AddPass(store, new DatedPass(0, henry.Id, 8, new ObservableCollection<PassAlteration>(),
            1, today.AddDays(-5), today.AddDays(40)));

        var sarahActiveCasual =
            AddPass(store, new CasualPass(0, sarahOConnor.Id, 10, new ObservableCollection<PassAlteration>(), 2));

        var islaActiveCasual =
            AddPass(store, new CasualPass(0, isla.Id, 10, new ObservableCollection<PassAlteration>(), 3));

        // --- Regular attendance roster (which students usually attend which class) --------------
        var roster = new Dictionary<ClassSchedule, (Student Student, Pass? Pass)[]>
        {
            [monMorning] = [(alice, aliceActiveDated), (tom, null)],
            [monEvening] = [(sarahOConnor, sarahActiveCasual), (isla, islaActiveCasual)],
            [tueMorning] = [(daniel, danielExpiredTerm), (grace, gracePassiveCasual)],
            [wedMorning] = [(ben, benActiveCasual), (grace, gracePassiveCasual)],
            [thuEvening] = [(emma, null), (isla, islaActiveCasual)],
            [friMorning] = [(chloe, chloeActiveTerm), (henry, henryActiveDated)]
        };

        // --- ~40-50 class roll occurrences over the last ~8 weeks plus a week ahead --------------
        for (var offset = -56; offset <= 7; offset++)
        {
            var date = today.AddDays(offset);

            foreach (var (schedule, attendees) in roster)
            {
                if (schedule.Day != date.DayOfWeek)
                    continue;

                var entries = attendees
                    .Select(a => new ClassRollEntry(a.Student, a.Pass))
                    .ToList();

                // Every third occurrence gets an extra walk-in student for variety.
                if ((date.DayNumber + schedule.Id) % 3 == 0)
                {
                    var walkIn = attendees[0].Student == henry ? isla : henry;
                    if (entries.All(e => e.Student.Id != walkIn.Id))
                        entries.Add(new ClassRollEntry(walkIn, null));
                }

                AddRoll(store, schedule, date, entries);
            }
        }
    }

    private static ClassSchedule AddSchedule(InMemoryDataStore store, DayOfWeek day, TimeOnly time,
        bool isArchived = false)
    {
        var schedule = new ClassSchedule(store.NextId(), day, time, isArchived);
        store.ClassSchedules[schedule.Id] = schedule;
        return schedule;
    }

    private static Term AddTerm(InMemoryDataStore store, string name, DateOnly start, DateOnly end,
        DateOnly? catchupStart, DateOnly? catchupEnd)
    {
        var term = new Term(store.NextId(), name, start, end, catchupStart, catchupEnd, []);
        store.Terms[term.Id] = term;
        return term;
    }

    private static Identity AddIdentity(InMemoryDataStore store, string firstName, string? lastName, string? phone,
        string? email)
    {
        var identity = new Identity(store.NextId(), firstName, lastName, phone, email, true);
        store.People[identity.Id] = identity;
        return identity;
    }

    private static Student AddStudent(InMemoryDataStore store, string firstName, string lastName, string? phone,
        string? email)
    {
        var student = new Student(store.NextId(), firstName, lastName, phone, email, true);
        store.People[student.Id] = student;
        return student;
    }

    private static void LinkContact(InMemoryDataStore store, Student student, Identity contact,
        Relationship relationship)
    {
        store.EmergencyContactLinks.Add(new EmergencyContactLink(student.Id, contact.Id, relationship));
    }

    private static Pass AddPass(InMemoryDataStore store, Pass pass)
    {
        pass.Id = store.NextId();
        store.Passes[pass.Id] = pass;
        return pass;
    }

    private static void AddAlteration(InMemoryDataStore store, Pass pass, int amount, string reason)
    {
        var alteration = new PassAlteration(store.NextId(), pass.Id, amount, reason);
        store.Alterations[alteration.Id] = alteration;
        pass.Alterations.Add(alteration);
    }

    private static void AddRoll(InMemoryDataStore store, ClassSchedule schedule, DateOnly date,
        List<ClassRollEntry> entries)
    {
        var roll = new ClassRoll(store.NextId(), date, schedule, entries);
        store.ClassRolls[roll.Id] = roll;
    }
}
