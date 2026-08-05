using System.Collections.ObjectModel;
using Dapper;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Hydration;

/// <summary>Batch-loads and hydrates Pass instances by id, keeping any caller that needs a handful of
/// passes (ClassRoll's StudentEntries, Pass's own Query) at 2 SQL round trips (one PassStatus query,
/// one batched PassAlterations query) regardless of how many distinct passes are involved - never N+1.</summary>
internal static class PassBatchLoader
{
    public static async Task<Dictionary<int, Pass>> LoadAsync(SqliteDataStore store, IReadOnlyCollection<int> passIds,
        CancellationToken cancellationToken = default)
    {
        if (passIds.Count == 0)
            return new Dictionary<int, Pass>();

        var statusRows = await store.QueryAsync(connection => connection.QueryAsync<PassStatusRow>(
            "SELECT * FROM PassStatus WHERE PassId IN @ids", new { ids = passIds }), cancellationToken);

        return await HydrateAsync(store, statusRows.ToList(), cancellationToken);
    }

    public static async Task<Pass?> LoadSingleAsync(SqliteDataStore store, int passId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(store, [passId], cancellationToken);
        return loaded.GetValueOrDefault(passId);
    }

    /// <summary>Hydrates a set of already-fetched PassStatus rows (e.g. from a filtered/sorted/paged
    /// Query) without re-querying PassStatus - just the one additional batched alterations query.</summary>
    public static async Task<Dictionary<int, Pass>> HydrateAsync(SqliteDataStore store,
        IReadOnlyList<PassStatusRow> statusRows, CancellationToken cancellationToken = default)
    {
        if (statusRows.Count == 0)
            return new Dictionary<int, Pass>();

        var passIds = statusRows.Select(r => r.PassId).ToList();

        var alterationRows = await store.QueryAsync(connection => connection.QueryAsync<PassAlterationRow>(
            "SELECT PassAlterationId, PassId, AlerationCount, AlterationReason FROM PassAlterations WHERE PassId IN @ids",
            new { ids = passIds }), cancellationToken);

        var alterationsByPass = alterationRows
            .GroupBy(a => a.PassId)
            .ToDictionary(g => g.Key, g => new ObservableCollection<PassAlteration>(
                g.Select(a => new PassAlteration(a.PassAlterationId, a.PassId, a.AlerationCount, a.AlterationReason ?? ""))));

        return statusRows.ToDictionary(row => row.PassId,
            row => PassHydrator.Hydrate(row, alterationsByPass.GetValueOrDefault(row.PassId) ?? []));
    }
}
