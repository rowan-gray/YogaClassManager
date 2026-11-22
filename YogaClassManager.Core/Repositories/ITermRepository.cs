using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Repositories;

public interface ITermRepository
{
    IReadOnlyDbModel<Term, TermFilter> Query(TermFilter filter);

    Task<int> AddAsync(Term term, CancellationToken cancellationToken = default);
    /// <summary>Saves the term's own details (name, dates, catchup dates). <paramref name="term" />'s Classes
    /// are ignored - linked classes change only via <see cref="LinkClassAsync" />/<see cref="UnlinkClassAsync" />,
    /// so a stale or empty Classes list can't silently drop links (or bypass UnlinkClassAsync's used-class guard).</summary>
    Task UpdateAsync(Term term, CancellationToken cancellationToken = default);

    /// <summary>Returns false (no-op) if the term still has any linked classes.</summary>
    Task<bool> TryDeleteAsync(int termId, CancellationToken cancellationToken = default);

    Task LinkClassAsync(int termId, int classScheduleId, int classCount, CancellationToken cancellationToken = default);

    /// <summary>No-ops if the linked class's Uses &gt; 0 - unlink only classes that haven't been used yet.</summary>
    Task<bool> UnlinkClassAsync(int termId, int classScheduleId, CancellationToken cancellationToken = default);
}
