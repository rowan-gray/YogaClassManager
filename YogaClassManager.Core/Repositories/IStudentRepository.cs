using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Repositories;

public interface IStudentRepository
{
    IDbModel<Student, StudentFilter> Query(StudentFilter filter);

    /// <summary>Transactionally creates the backing Identity row (if needed) and the Student row.</summary>
    Task<int> AddAsync(Student student, CancellationToken cancellationToken = default);
    Task UpdateAsync(Student student, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Converts an existing, non-Student Identity into a Student in place, preserving its id (and
    ///     therefore any emergency-contact links pointing at it). <paramref name="student" />.Id must
    ///     identify an existing Identity row. Throws <see cref="InvalidOperationException" /> if that
    ///     Identity doesn't exist or is already a Student - mirrors <see cref="IIdentityRepository.MergeAsync" />'s
    ///     "both Students" guard, since promoting an already-Student row is equally ambiguous.
    /// </summary>
    Task<int> PromoteToStudentAsync(Student student, CancellationToken cancellationToken = default);

    /// <summary>Delegates to <see cref="IIdentityRepository.ArchiveOrDeleteAsync" /> - a Student shares its Identity row.</summary>
    Task<ArchiveResult> ArchiveOrDeleteAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Delegates to <see cref="IIdentityRepository.UnarchiveAsync" /> - a Student shares its Identity row.</summary>
    Task UnarchiveAsync(int studentId, CancellationToken cancellationToken = default);

    Task LinkEmergencyContactAsync(int studentId, int identityId, Relationship relationship,
        CancellationToken cancellationToken = default);
    Task UnlinkEmergencyContactAsync(int studentId, int identityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmergencyContact>> GetEmergencyContactsAsync(int studentId,
        CancellationToken cancellationToken = default);

    Task AddHealthConcernAsync(int studentId, string concern, CancellationToken cancellationToken = default);
    Task RemoveHealthConcernAsync(int studentId, string concern, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetHealthConcernsAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Pass>> GetPassesAsync(int studentId, bool includeExpired = false,
        bool includeDepleted = false, CancellationToken cancellationToken = default);
}
