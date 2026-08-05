using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Repositories;

public interface IClassScheduleRepository
{
    IDbModel<ClassSchedule, ClassScheduleFilter> Query(ClassScheduleFilter filter);

    /// <summary>Throws <see cref="InvalidOperationException" /> if Day/Time collides with an existing schedule.</summary>
    Task<int> AddAsync(ClassSchedule schedule, CancellationToken cancellationToken = default);
    Task UpdateAsync(ClassSchedule schedule, CancellationToken cancellationToken = default);

    Task ArchiveAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task UnarchiveAsync(int scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Returns false (no-op) if the schedule is referenced by any ClassRoll or TermClassSchedule - archive instead.</summary>
    Task<bool> TryDeleteAsync(int scheduleId, CancellationToken cancellationToken = default);
}
