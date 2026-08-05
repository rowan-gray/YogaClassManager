using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Core.Repositories;

public interface IPassRepository
{
    IDbModel<Pass, PassFilter> Query(PassFilter filter);

    Task<int> AddAsync(Pass pass, CancellationToken cancellationToken = default);
    Task UpdateAsync(Pass pass, CancellationToken cancellationToken = default);
    Task DeleteAsync(int passId, CancellationToken cancellationToken = default);

    Task AddAlterationAsync(PassAlteration alteration, CancellationToken cancellationToken = default);
    Task RemoveAlterationAsync(int alterationId, CancellationToken cancellationToken = default);

    Task<Pass?> GetByIdAsync(int passId, CancellationToken cancellationToken = default);

    /// <summary>The class occurrences this pass has been used for ("what classes was this pass used for").</summary>
    Task<IReadOnlyList<ClassAttendanceRecord>> GetUsageHistoryAsync(int passId,
        CancellationToken cancellationToken = default);
}
