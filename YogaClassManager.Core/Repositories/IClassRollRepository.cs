using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Repositories;

public interface IClassRollRepository
{
    IDbModel<ClassRoll, ClassRollFilter> Query(ClassRollFilter filter);

    Task<int> AddAsync(ClassRoll roll, CancellationToken cancellationToken = default);
    Task UpdateDetailsAsync(ClassRoll roll, CancellationToken cancellationToken = default);
    Task DeleteAsync(int classRollId, CancellationToken cancellationToken = default);

    Task AddStudentEntryAsync(int classRollId, ClassRollEntry entry, CancellationToken cancellationToken = default);
    Task RemoveStudentEntryAsync(int classRollId, int studentId, CancellationToken cancellationToken = default);
    Task UpdateStudentEntryPassAsync(int classRollId, int studentId, int? passId,
        CancellationToken cancellationToken = default);

    /// <summary>The class occurrences this student has attended ("what classes has this student attended").</summary>
    Task<IReadOnlyList<ClassAttendanceRecord>> GetAttendanceHistoryAsync(int studentId,
        CancellationToken cancellationToken = default);
}
