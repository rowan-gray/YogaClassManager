using Dapper;
using Microsoft.Data.Sqlite;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Hydration;

namespace YogaClassManager.Core.SQLite.Repositories;

public class SqlitePassRepository : IPassRepository
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqlitePassRepository(IDataStoreProvider dataStoreProvider)
    {
        this.dataStoreProvider = dataStoreProvider;
    }

    public IDbModel<Pass, PassFilter> Query(PassFilter filter)
    {
        return new SqlitePassDbModel(filter, dataStoreProvider);
    }

    public Task<int> AddAsync(Pass pass, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) => InsertAsync(connection, transaction, pass),
            cancellationToken);
    }

    public Task UpdateAsync(Pass pass, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(
            (connection, transaction) => UpdateInternalAsync(connection, transaction, pass), cancellationToken);
    }

    public Task DeleteAsync(int passId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            // ClassStudents.PassId's FK has no ON DELETE action, so with foreign_keys=ON a bare
            // DELETE FROM Pass while any ClassStudents row still points at it would fail with a FK
            // constraint error. Null out the references first (the attendance record itself stays,
            // it just loses its pass link), then delete - the Pass row's own delete cascades to its
            // subtype table and PassAlterations automatically (both ON DELETE CASCADE).
            await connection.ExecuteAsync("UPDATE ClassStudents SET PassId=NULL WHERE PassId=$id", new { id = passId },
                transaction);
            await connection.ExecuteAsync("DELETE FROM Pass WHERE PassId=$id", new { id = passId }, transaction);
        }, cancellationToken);
    }

    public Task AddAlterationAsync(PassAlteration alteration, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO PassAlterations(PassId, AlerationCount, AlterationReason)
                VALUES ($passId, $amount, $reason);
                SELECT last_insert_rowid();
                """,
                new { passId = alteration.PassId, amount = alteration.Amount, reason = alteration.Reason }, transaction);
            alteration.Id = (int)id;
        }, cancellationToken);
    }

    public Task RemoveAlterationAsync(int alterationId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM PassAlterations WHERE PassAlterationId=$id", new { id = alterationId },
                transaction), cancellationToken);
    }

    public Task<Pass?> GetByIdAsync(int passId, CancellationToken cancellationToken = default)
    {
        return Query(new PassFilter { Id = (uint)passId, IncludeExpired = true, IncludeDepleted = true })
            .LoadSingle(cancellationToken);
    }

    public async Task<IReadOnlyList<ClassAttendanceRecord>> GetUsageHistoryAsync(int passId,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var rows = await store.QueryAsync(connection => connection.QueryAsync<ClassAttendanceRow>(
            """
            SELECT cr.ClassId AS ClassRollId, cr.Date AS Date, cs.ClassScheduleId AS ClassScheduleId,
                   cs.Day AS Day, cs.Time AS Time, cs.IsActive AS IsActive,
                   csx.StudentId AS StudentId, csx.PassId AS PassId
            FROM ClassStudents csx
            JOIN ClassRoll cr ON cr.ClassId = csx.ClassId
            JOIN ClassSchedule cs ON cs.ClassScheduleId = cr.ClassScheduleId
            WHERE csx.PassId = $passId
            ORDER BY cr.Date DESC
            """, new { passId }), cancellationToken);

        return rows.Select(r => r.ToRecord()).ToList();
    }

    internal static async Task<int> InsertAsync(SqliteConnection connection, SqliteTransaction transaction, Pass pass)
    {
        var id = await connection.ExecuteScalarAsync<long>(
            "INSERT INTO Pass(StudentId) VALUES ($studentId); SELECT last_insert_rowid();",
            new { studentId = pass.StudentId }, transaction);
        pass.Id = (int)id;

        await InsertSubtypeRowAsync(connection, transaction, pass);
        await InsertAlterationsAsync(connection, transaction, pass);

        return pass.Id;
    }

    private static async Task InsertSubtypeRowAsync(SqliteConnection connection, SqliteTransaction transaction, Pass pass)
    {
        switch (pass)
        {
            case CasualPass casual:
                await connection.ExecuteAsync("INSERT INTO CasualPass(PassId, ClassCount) VALUES ($id, $classCount)",
                    new { id = pass.Id, classCount = casual.ClassCount }, transaction);
                break;
            case DatedPass dated:
                await connection.ExecuteAsync(
                    "INSERT INTO DatedPass(PassId, ClassCount, StartDate, EndDate) VALUES ($id, $classCount, $startDate, $endDate)",
                    new { id = pass.Id, classCount = dated.ClassCount, startDate = dated.StartDate, endDate = dated.EndDate },
                    transaction);
                break;
            case TermPass term:
                await connection.ExecuteAsync(
                    "INSERT INTO TermPass(PassId, TermId, ClassId) VALUES ($id, $termId, $classId)",
                    new { id = pass.Id, termId = term.Term.Id, classId = term.TermClassSchedule.ClassSchedule.Id },
                    transaction);
                break;
            default:
                throw new InvalidOperationException($"Unsupported Pass subtype '{pass.GetType()}'.");
        }
    }

    private static async Task InsertAlterationsAsync(SqliteConnection connection, SqliteTransaction transaction, Pass pass)
    {
        foreach (var alteration in pass.Alterations)
        {
            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO PassAlterations(PassId, AlerationCount, AlterationReason)
                VALUES ($passId, $amount, $reason);
                SELECT last_insert_rowid();
                """,
                new { passId = pass.Id, amount = alteration.Amount, reason = alteration.Reason }, transaction);
            alteration.Id = (int)id;
            alteration.PassId = pass.Id;
        }
    }

    private static string KindOf(Pass pass)
    {
        return pass switch
        {
            CasualPass => "Casual",
            DatedPass => "Dated",
            TermPass => "Term",
            _ => throw new InvalidOperationException($"Unsupported Pass subtype '{pass.GetType()}'.")
        };
    }

    /// <summary>The identity-preserving semantics here are an explicit regression test (the original
    /// MAUI app's SavePassAsync never persisted edits to CasualPass.ClassCount): when the pass's
    /// concrete type is unchanged, update the matching subtype row's own columns in place and replace
    /// Alterations; when the type changed, replace the subtype row wholesale. Never writes
    /// ClassesUsed - it's a derived value (see PassStatus view), not stored.</summary>
    internal static async Task UpdateInternalAsync(SqliteConnection connection, SqliteTransaction transaction, Pass pass)
    {
        if (pass.Id <= 0)
        {
            await InsertAsync(connection, transaction, pass);
            return;
        }

        var passExists = await connection.ExecuteScalarAsync<long>(
            "SELECT EXISTS(SELECT 1 FROM Pass WHERE PassId=$id)", new { id = pass.Id }, transaction) == 1;

        if (!passExists)
        {
            await connection.ExecuteAsync("INSERT INTO Pass(PassId, StudentId) VALUES ($id, $studentId)",
                new { id = pass.Id, studentId = pass.StudentId }, transaction);
            await InsertSubtypeRowAsync(connection, transaction, pass);
            await InsertAlterationsAsync(connection, transaction, pass);
            return;
        }

        var existingKind = await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT Kind FROM PassDetails WHERE PassId=$id", new { id = pass.Id }, transaction);

        var incomingKind = KindOf(pass);

        if (existingKind != incomingKind)
        {
            await connection.ExecuteAsync("DELETE FROM CasualPass WHERE PassId=$id", new { id = pass.Id }, transaction);
            await connection.ExecuteAsync("DELETE FROM DatedPass WHERE PassId=$id", new { id = pass.Id }, transaction);
            await connection.ExecuteAsync("DELETE FROM TermPass WHERE PassId=$id", new { id = pass.Id }, transaction);
            await connection.ExecuteAsync("UPDATE Pass SET StudentId=$studentId WHERE PassId=$id",
                new { id = pass.Id, studentId = pass.StudentId }, transaction);
            await InsertSubtypeRowAsync(connection, transaction, pass);
        }
        else
        {
            switch (pass)
            {
                case CasualPass casual:
                    await connection.ExecuteAsync("UPDATE CasualPass SET ClassCount=$classCount WHERE PassId=$id",
                        new { id = pass.Id, classCount = casual.ClassCount }, transaction);
                    break;
                case DatedPass dated:
                    await connection.ExecuteAsync(
                        "UPDATE DatedPass SET ClassCount=$classCount, StartDate=$startDate, EndDate=$endDate WHERE PassId=$id",
                        new { id = pass.Id, classCount = dated.ClassCount, startDate = dated.StartDate, endDate = dated.EndDate },
                        transaction);
                    break;
                case TermPass term:
                    await connection.ExecuteAsync("UPDATE TermPass SET TermId=$termId, ClassId=$classId WHERE PassId=$id",
                        new { id = pass.Id, termId = term.Term.Id, classId = term.TermClassSchedule.ClassSchedule.Id },
                        transaction);
                    break;
            }
        }

        // Alterations: replace wholesale - ids are owned entirely by the pass, nothing external
        // references them, so there's no need to diff.
        await connection.ExecuteAsync("DELETE FROM PassAlterations WHERE PassId=$id", new { id = pass.Id }, transaction);
        foreach (var alteration in pass.Alterations)
        {
            var newId = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO PassAlterations(PassId, AlerationCount, AlterationReason)
                VALUES ($passId, $amount, $reason);
                SELECT last_insert_rowid();
                """,
                new { passId = pass.Id, amount = alteration.Amount, reason = alteration.Reason }, transaction);
            alteration.Id = (int)newId;
        }
    }
}

internal class SqlitePassDbModel : IDbModel<Pass, PassFilter>
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqlitePassDbModel(PassFilter filter, IDataStoreProvider dataStoreProvider)
    {
        Filter = filter;
        this.dataStoreProvider = dataStoreProvider;
    }

    public PassFilter Filter { get; init; }

    public async Task<Pass?> LoadSingle(CancellationToken cancellationToken = default)
    {
        var results = await LoadMultiple(1, 0, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Pass>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var filter = Filter;
        var take = count > int.MaxValue ? int.MaxValue : (int)count;

        var direction = filter.SortBy is null
            ? "ASC"
            : StringValueAttribute.GetStringValue(filter.SortBy.Value.Value);

        var sql = $"""
                   SELECT * FROM PassStatus
                   WHERE ($id IS NULL OR PassId = $id)
                     AND ($studentId IS NULL OR StudentId = $studentId)
                     AND ($kind = 'Any' OR Kind = $kind)
                     AND ($includeExpired = 1 OR IsExpired = 0)
                     AND ($includeDepleted = 1 OR IsDepleted = 0)
                   ORDER BY PassId {direction}
                   LIMIT $take OFFSET $skip
                   """;

        var rows = (await store.QueryAsync(connection => connection.QueryAsync<PassStatusRow>(sql, new
        {
            id = (int?)filter.Id,
            studentId = filter.StudentId,
            kind = filter.Kind.ToString(),
            includeExpired = filter.IncludeExpired ? 1 : 0,
            includeDepleted = filter.IncludeDepleted ? 1 : 0,
            take,
            skip = (int)skip
        }), cancellationToken)).ToList();

        // Sequential, not nested: the PassStatus query above already fully completed and released
        // SqliteDataStore's gate before this second call acquires it - never nest one store.QueryAsync
        // call inside another's delegate, since the gate isn't reentrant.
        var hydrated = await PassBatchLoader.HydrateAsync(store, rows, cancellationToken);

        return rows.Select(r => hydrated[r.PassId]).ToList();
    }

    public async Task<bool> Refresh(Pass model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var statusRow = await store.QueryAsync(connection => connection.QuerySingleOrDefaultAsync<PassStatusRow>(
            "SELECT * FROM PassStatus WHERE PassId=$id", new { id = model.Id }), cancellationToken);

        if (statusRow is null)
            return false;

        var hydrated = await PassBatchLoader.HydrateAsync(store, [statusRow], cancellationToken);
        var fresh = hydrated[model.Id];

        model.ClassesUsed = fresh.ClassesUsed;
        model.Alterations.Clear();
        foreach (var alteration in fresh.Alterations)
            model.Alterations.Add(alteration);

        switch (model)
        {
            case CasualPass casual when fresh is CasualPass freshCasual:
                casual.ClassCount = freshCasual.ClassCount;
                break;
            case DatedPass dated when fresh is DatedPass freshDated:
                dated.ClassCount = freshDated.ClassCount;
                dated.StartDate = freshDated.StartDate;
                dated.EndDate = freshDated.EndDate;
                break;
            case TermPass term when fresh is TermPass freshTerm:
                term.Term = freshTerm.Term;
                term.TermClassSchedule = freshTerm.TermClassSchedule;
                break;
        }

        return true;
    }

    public Task Save(Pass model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var exists = model.Id > 0 && await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Pass WHERE PassId=$id)", new { id = model.Id }, transaction) == 1;

            if (exists && saveOptions == SaveOptions.Create)
                throw new ModelExistsException();
            if (!exists && saveOptions == SaveOptions.Replace)
                throw new ModelDoesNotExistException();

            if (exists)
                await SqlitePassRepository.UpdateInternalAsync(connection, transaction, model);
            else
                await SqlitePassRepository.InsertAsync(connection, transaction, model);
        }, cancellationToken);
    }

    public Task Delete(Pass model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await connection.ExecuteAsync("UPDATE ClassStudents SET PassId=NULL WHERE PassId=$id", new { id = model.Id },
                transaction);
            await connection.ExecuteAsync("DELETE FROM Pass WHERE PassId=$id", new { id = model.Id }, transaction);
        }, cancellationToken);
    }
}
