using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Repositories;

public interface IIdentityRepository
{
    IDbModel<Identity, IdentityFilter> Query(IdentityFilter filter);

    Task<int> AddAsync(Identity identity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Identity identity, CancellationToken cancellationToken = default);

    Task<IdentityLinkageSummary> GetLinkageSummaryAsync(int identityId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Archives the identity (soft IsActive=false) unless they have no links to any other record
    ///     (not a Student, not anyone's emergency contact, no passes, no attendance), in which case
    ///     the Identity row is hard-deleted instead.
    /// </summary>
    Task<ArchiveResult> ArchiveOrDeleteAsync(int identityId, CancellationToken cancellationToken = default);

    Task UnarchiveAsync(int identityId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Merges <paramref name="duplicateIdentityId" /> into <paramref name="survivingIdentityId" />: repoints every
    ///     emergency-contact link, and - if the duplicate is a Student - every pass/attendance record and health
    ///     concern, then deletes the now-unlinked duplicate. Throws <see cref="InvalidOperationException" /> if both
    ///     identities are Students (ambiguous - archive one and re-link manually instead).
    /// </summary>
    Task<MergeResult> MergeAsync(int survivingIdentityId, int duplicateIdentityId,
        CancellationToken cancellationToken = default);
}
