using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Repositories;

public interface IClassRollRepository
{
    IReadOnlyDbModel<ClassRoll, ClassRollFilter> Query(ClassRollFilter filter);

    Task<int> AddAsync(ClassRoll roll, CancellationToken cancellationToken = default);
    Task UpdateDetailsAsync(ClassRoll roll, CancellationToken cancellationToken = default);
    Task DeleteAsync(int classRollId, CancellationToken cancellationToken = default);

    Task AddStudentEntryAsync(int classRollId, ClassRollEntry entry, CancellationToken cancellationToken = default);
    Task RemoveStudentEntryAsync(int classRollId, int studentId, CancellationToken cancellationToken = default);
    Task UpdateStudentEntryPassAsync(int classRollId, int studentId, int? passId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves an edit session on an existing roll all-or-nothing: the roll's own details
    /// (<paramref name="details" />' Date/ClassSchedule - its StudentEntries are ignored, as with
    /// <see cref="UpdateDetailsAsync" />), then removes <paramref name="removedStudentIds" />, then adds
    /// or re-passes <paramref name="addedOrChangedEntries" /> (each upserted by student, as with
    /// <see cref="AddStudentEntryAsync" />). If any part fails, none of it is applied.
    ///
    /// Deliberately a set of changes rather than "replace the attendee list": attendees the caller didn't
    /// touch are left alone, so one added to the roll elsewhere while it was being edited survives.</summary>
    Task ApplyChangesAsync(ClassRoll details, IReadOnlyCollection<int> removedStudentIds,
        IReadOnlyCollection<ClassRollEntry> addedOrChangedEntries, CancellationToken cancellationToken = default);

    /// <summary>The class occurrences this student has attended ("what classes has this student
    /// attended"), filtered/sorted by the same <see cref="ClassRollFilter" /> fields (DateFrom/DateTo/
    /// DayOfWeek/SortBy) as <see cref="Query" /> - Id/ClassScheduleId are meaningless here and
    /// ignored. Kept as the same filter shape so this list's filtering/ordering can't drift from the
    /// Class Rolls list's.</summary>
    Task<IReadOnlyList<ClassAttendanceRecord>> GetAttendanceHistoryAsync(int studentId,
        ClassRollFilter filter = default, CancellationToken cancellationToken = default);
}
