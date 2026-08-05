using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Tests.Shared;

/// <summary>
///     Constructs a fresh, isolated set of repositories for one test. Each abstract test base takes
///     one of these (via a generic type parameter, constructed by its thin backend-specific
///     subclass) instead of constructing an InMemoryDataStore/InMemoryXxxRepository or
///     SqliteDataStore/SqliteXxxRepository directly, so the exact same test body can run against
///     either backend.
///
///     Almost every fixture the shared test bases need can be built using these repositories' own
///     public methods (AddAsync, LinkClassAsync, etc.) exactly as a real caller would. Two cases need
///     a backend-specific hook instead, because the two backends model "already used" state in
///     genuinely incompatible ways with no common sequence of public calls that produces the same
///     observable result on both:
///     <list type="bullet">
///         <item><see cref="SeedTermClassUsageAsync" />: the in-memory backend's TermClassSchedule.Uses
///         is a free-standing settable int never incremented by adding a TermPass, while the SQLite
///         backend derives Uses entirely from real TermPass rows (via the TermClassUses view).</item>
///         <item><see cref="SeedPassUsageAsync" />: the in-memory backend's Pass.ClassesUsed is a
///         free-standing settable field (whatever value the constructor/UpdateAsync last wrote), while
///         the SQLite backend derives it entirely from real ClassStudents attendance rows (via the
///         PassUses/PassStatus views) - see the "Pass.ClassesUsed is derived, not stored" decision.</item>
///     </list>
/// </summary>
public interface IRepositoryTestFactory : IAsyncDisposable
{
    Task InitializeAsync();

    IClassRollRepository ClassRolls { get; }
    IClassScheduleRepository ClassSchedules { get; }
    IEmergencyContactRepository EmergencyContacts { get; }
    IIdentityRepository Identities { get; }
    IPassRepository Passes { get; }
    IStudentRepository Students { get; }
    ITermRepository Terms { get; }

    /// <summary>Makes the given (already-linked, via ITermRepository.LinkClassAsync) TermId/ClassId
    /// pair show <paramref name="usesCount" /> for TermClassSchedule.Uses, using whichever mechanism
    /// is correct for the backend (direct field set for in-memory; real TermPass rows for SQLite).</summary>
    Task SeedTermClassUsageAsync(int termId, int classScheduleId, int usesCount);

    /// <summary>Makes the given pass (owned by <paramref name="studentId" />) show
    /// <paramref name="usesCount" /> for ClassesUsed, using whichever mechanism is correct for the
    /// backend (direct field set for in-memory; real ClassRoll/ClassStudents attendance rows for SQLite).</summary>
    Task SeedPassUsageAsync(int passId, int studentId, int usesCount);
}
