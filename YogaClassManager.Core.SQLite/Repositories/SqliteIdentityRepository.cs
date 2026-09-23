using Dapper;
using Microsoft.Data.Sqlite;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Repositories;

public class SqliteIdentityRepository : IIdentityRepository
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteIdentityRepository(IDataStoreProvider dataStoreProvider)
    {
        this.dataStoreProvider = dataStoreProvider;
    }

    public IDbModel<Identity, IdentityFilter> Query(IdentityFilter filter)
    {
        return new SqliteIdentityDbModel(filter, dataStoreProvider);
    }

    public Task<int> AddAsync(Identity identity, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO Person(FirstName, LastName, PhoneNumber, Email, IsActive)
                VALUES ($firstName, $lastName, $phoneNumber, $email, $isActive);
                SELECT last_insert_rowid();
                """,
                new
                {
                    firstName = identity.FirstName, lastName = identity.LastName, phoneNumber = identity.PhoneNumber,
                    email = identity.Email, isActive = identity.IsActive ? 1 : 0
                }, transaction);

            identity.Id = (int)id;
            return identity.Id;
        }, cancellationToken);
    }

    public Task UpdateAsync(Identity identity, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync(
                """
                UPDATE Person SET FirstName=$firstName, LastName=$lastName, PhoneNumber=$phoneNumber,
                                   Email=$email, IsActive=$isActive
                WHERE PersonId=$id
                """,
                new
                {
                    id = identity.Id, firstName = identity.FirstName, lastName = identity.LastName,
                    phoneNumber = identity.PhoneNumber, email = identity.Email, isActive = identity.IsActive ? 1 : 0
                }, transaction), cancellationToken);
    }

    public async Task<IdentityLinkageSummary> GetLinkageSummaryAsync(int identityId,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var row = await store.QueryAsync(connection => connection.QuerySingleOrDefaultAsync<IdentityLinkageRow>(
            "SELECT IsStudent, EmergencyContactLinkCount, PassCount, AttendanceCount FROM IdentityLinkage WHERE PersonId=$id",
            new { id = identityId }), cancellationToken);

        return row is null
            ? new IdentityLinkageSummary(false, 0, 0, 0)
            : new IdentityLinkageSummary(row.IsStudent == 1, row.EmergencyContactLinkCount, row.PassCount,
                row.AttendanceCount);
    }

    public Task<ArchiveResult> ArchiveOrDeleteAsync(int identityId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(
            (connection, transaction) => ArchiveOrDeleteInternalAsync(connection, transaction, identityId),
            cancellationToken);
    }

    public Task UnarchiveAsync(int identityId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("UPDATE Person SET IsActive=1 WHERE PersonId=$id", new { id = identityId },
                transaction), cancellationToken);
    }

    public Task<MergeResult> MergeAsync(int survivingIdentityId, int duplicateIdentityId,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var survivorExists = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Person WHERE PersonId=$id)", new { id = survivingIdentityId }, transaction) == 1;
            if (!survivorExists)
                throw new InvalidOperationException($"Identity {survivingIdentityId} does not exist.");

            var duplicateExists = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Person WHERE PersonId=$id)", new { id = duplicateIdentityId }, transaction) == 1;
            if (!duplicateExists)
                throw new InvalidOperationException($"Identity {duplicateIdentityId} does not exist.");

            var survivorIsStudent = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Student WHERE StudentId=$id)", new { id = survivingIdentityId },
                transaction) == 1;
            var duplicateIsStudent = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Student WHERE StudentId=$id)", new { id = duplicateIdentityId },
                transaction) == 1;

            if (survivorIsStudent && duplicateIsStudent)
                throw new InvalidOperationException(
                    "Cannot merge two Students - archive one and re-link their relationships manually instead.");

            // Promote the survivor to a Student BEFORE repointing any link, if the duplicate is a
            // Student - StudentEmergencyContacts.StudentId/Pass.StudentId/ClassStudents.StudentId/
            // StudentHealthConcerns.StudentId all have FK constraints requiring the target to already
            // be a Student row. This is a deliberate reordering vs. the in-memory backend (which
            // promotes AFTER repointing, since it has no such constraint to satisfy).
            if (duplicateIsStudent && !survivorIsStudent)
                await connection.ExecuteAsync("INSERT INTO Student(StudentId) VALUES ($id)",
                    new { id = survivingIdentityId }, transaction);

            var repointedEmergencyContactLinks =
                await RepointEmergencyContactLinksAsync(connection, transaction, survivingIdentityId, duplicateIdentityId);

            var repointedPasses = 0;
            var repointedAttendance = 0;

            if (duplicateIsStudent)
            {
                repointedPasses = await connection.ExecuteAsync(
                    "UPDATE Pass SET StudentId=$survivor WHERE StudentId=$duplicate",
                    new { survivor = survivingIdentityId, duplicate = duplicateIdentityId }, transaction);

                // ClassStudents composite-PK conflict - new policy decision with no in-memory
                // precedent (its object-reference swap has no such conflict to resolve): if both
                // survivor and duplicate already have a row for the same ClassId (e.g. both attended
                // the same class before being recognized as the same person), keep the survivor's
                // existing row untouched and drop the duplicate's now-redundant row.
                await connection.ExecuteAsync(
                    """
                    DELETE FROM ClassStudents
                    WHERE StudentId = $duplicate
                      AND ClassId IN (SELECT ClassId FROM ClassStudents WHERE StudentId = $survivor)
                    """,
                    new { survivor = survivingIdentityId, duplicate = duplicateIdentityId }, transaction);

                repointedAttendance = await connection.ExecuteAsync(
                    "UPDATE ClassStudents SET StudentId=$survivor WHERE StudentId=$duplicate",
                    new { survivor = survivingIdentityId, duplicate = duplicateIdentityId }, transaction);

                await connection.ExecuteAsync(
                    """
                    INSERT INTO StudentHealthConcerns(StudentId, HealthConcern)
                    SELECT $survivor, HealthConcern FROM StudentHealthConcerns
                    WHERE StudentId = $duplicate
                      AND HealthConcern NOT IN (SELECT HealthConcern FROM StudentHealthConcerns WHERE StudentId = $survivor)
                    """,
                    new { survivor = survivingIdentityId, duplicate = duplicateIdentityId }, transaction);
                await connection.ExecuteAsync("DELETE FROM StudentHealthConcerns WHERE StudentId=$duplicate",
                    new { duplicate = duplicateIdentityId }, transaction);

                // Demotes the duplicate back to a plain Person - Pass/ClassStudents/HealthConcerns
                // have all been repointed/removed above already, satisfying Pass's ON DELETE RESTRICT FK.
                await connection.ExecuteAsync("DELETE FROM Student WHERE StudentId=$id",
                    new { id = duplicateIdentityId }, transaction);
            }

            // Everything Student-specific has been repointed/removed - this typically hard-deletes the
            // duplicate's Person row now, unless it's still linked as someone else's emergency contact.
            await ArchiveOrDeleteInternalAsync(connection, transaction, duplicateIdentityId);

            return new MergeResult(survivingIdentityId, duplicateIdentityId, repointedEmergencyContactLinks,
                repointedAttendance, repointedPasses);
        }, cancellationToken);
    }

    private static async Task<int> RepointEmergencyContactLinksAsync(SqliteConnection connection,
        SqliteTransaction transaction, int survivorId, int duplicateId)
    {
        var touchedAsContact = await connection.ExecuteAsync(
            """
            UPDATE StudentEmergencyContacts
            SET EmergencyContactId = $survivor
            WHERE EmergencyContactId = $duplicate
              AND NOT EXISTS (
                  SELECT 1 FROM StudentEmergencyContacts x
                  WHERE x.StudentId = StudentEmergencyContacts.StudentId AND x.EmergencyContactId = $survivor)
            """,
            new { survivor = survivorId, duplicate = duplicateId }, transaction);

        await connection.ExecuteAsync("DELETE FROM StudentEmergencyContacts WHERE EmergencyContactId = $duplicate",
            new { duplicate = duplicateId }, transaction);

        var touchedAsStudent = await connection.ExecuteAsync(
            """
            UPDATE StudentEmergencyContacts
            SET StudentId = $survivor
            WHERE StudentId = $duplicate
              AND NOT EXISTS (
                  SELECT 1 FROM StudentEmergencyContacts x
                  WHERE x.StudentId = $survivor AND x.EmergencyContactId = StudentEmergencyContacts.EmergencyContactId)
            """,
            new { survivor = survivorId, duplicate = duplicateId }, transaction);

        await connection.ExecuteAsync("DELETE FROM StudentEmergencyContacts WHERE StudentId = $duplicate",
            new { duplicate = duplicateId }, transaction);

        return touchedAsContact + touchedAsStudent;
    }

    private static async Task<ArchiveResult> ArchiveOrDeleteInternalAsync(SqliteConnection connection,
        SqliteTransaction transaction, int identityId)
    {
        var row = await connection.QuerySingleOrDefaultAsync<IdentityLinkageRow>(
            "SELECT IsStudent, EmergencyContactLinkCount, PassCount, AttendanceCount FROM IdentityLinkage WHERE PersonId=$id",
            new { id = identityId }, transaction);

        var hasAnyLinks = row is not null &&
                           (row.IsStudent == 1 || row.EmergencyContactLinkCount > 0 || row.PassCount > 0 ||
                            row.AttendanceCount > 0);

        if (!hasAnyLinks)
        {
            await connection.ExecuteAsync("DELETE FROM Person WHERE PersonId=$id", new { id = identityId }, transaction);
            return ArchiveResult.Deleted;
        }

        await connection.ExecuteAsync("UPDATE Person SET IsActive=0 WHERE PersonId=$id", new { id = identityId },
            transaction);
        return ArchiveResult.Archived;
    }
}

internal sealed class IdentityLinkageRow
{
    public int IsStudent { get; set; }
    public int EmergencyContactLinkCount { get; set; }
    public int PassCount { get; set; }
    public int AttendanceCount { get; set; }
}

internal sealed class IdentityRow
{
    public int PersonId { get; set; }
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int IsActive { get; set; }

    public Identity ToModel()
    {
        return new Identity(PersonId, FirstName, LastName, PhoneNumber, Email, IsActive == 1);
    }
}

internal class SqliteIdentityDbModel : IDbModel<Identity, IdentityFilter>
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteIdentityDbModel(IdentityFilter filter, IDataStoreProvider dataStoreProvider)
    {
        Filter = filter;
        this.dataStoreProvider = dataStoreProvider;
    }

    public IdentityFilter Filter { get; init; }

    public async Task<Identity?> LoadSingle(CancellationToken cancellationToken = default)
    {
        var results = await LoadMultiple(1, 0, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Identity>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var filter = Filter;
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        var orderBy = BuildOrderBy(filter.SortBy);

        const string fullNameExpr = "(CASE WHEN LastName IS NULL THEN FirstName ELSE FirstName || ' ' || LastName END)";

        var sql = $"""
                   SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive
                   FROM Person
                   WHERE ($id IS NULL OR PersonId = $id)
                     AND ($nameFilter IS NULL OR
                          {fullNameExpr} LIKE $nameContains ESCAPE '\'
                          OR FirstName LIKE $namePrefix ESCAPE '\'
                          OR (LastName IS NOT NULL AND LastName LIKE $namePrefix ESCAPE '\'))
                     AND ($firstNameFilter IS NULL OR FirstName LIKE $firstNamePrefix ESCAPE '\')
                     AND ($lastNameFilter IS NULL OR (LastName IS NOT NULL AND LastName LIKE $lastNamePrefix ESCAPE '\'))
                     AND ($emailFilter IS NULL OR (Email IS NOT NULL AND Email LIKE $emailPrefix ESCAPE '\'))
                     AND ($phoneNumberFilter IS NULL OR (PhoneNumber IS NOT NULL AND PhoneNumber LIKE $phonePrefix ESCAPE '\'))
                     AND ($isActive IS NULL OR IsActive = $isActive)
                   ORDER BY {orderBy}
                   LIMIT $take OFFSET $skip
                   """;

        var rows = await store.QueryAsync(connection => connection.QueryAsync<IdentityRow>(sql, new
        {
            id = (int?)filter.Id,
            nameFilter = filter.NameFilter,
            nameContains = filter.NameFilter is null ? null : SqlFilterBuilder.ContainsPattern(filter.NameFilter),
            namePrefix = filter.NameFilter is null ? null : SqlFilterBuilder.StartsWithPattern(filter.NameFilter),
            firstNameFilter = filter.FirstNameFilter,
            firstNamePrefix = filter.FirstNameFilter is null ? null : SqlFilterBuilder.StartsWithPattern(filter.FirstNameFilter),
            lastNameFilter = filter.LastNameFilter,
            lastNamePrefix = filter.LastNameFilter is null ? null : SqlFilterBuilder.StartsWithPattern(filter.LastNameFilter),
            emailFilter = filter.EmailFilter,
            emailPrefix = filter.EmailFilter is null ? null : SqlFilterBuilder.StartsWithPattern(filter.EmailFilter),
            phoneNumberFilter = filter.PhoneNumberFilter,
            phonePrefix = filter.PhoneNumberFilter is null ? null : SqlFilterBuilder.StartsWithPattern(filter.PhoneNumberFilter),
            isActive = filter.IsActive is null ? null : (int?)(filter.IsActive.Value ? 1 : 0),
            take,
            skip = (int)skip
        }), cancellationToken);

        return rows.Select(r => r.ToModel()).ToList();
    }

    private static string BuildOrderBy(KeyValuePair<IdentitySortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return SqlFilterBuilder.AppendPkTiebreaker("PersonId ASC", "PersonId");

        var (key, order) = sortBy.Value;
        var direction = StringValueAttribute.GetStringValue(order);

        var column = key switch
        {
            IdentitySortOptions.FirstName => "FirstName",
            IdentitySortOptions.LastName => "LastName",
            IdentitySortOptions.PhoneNumber => "PhoneNumber",
            IdentitySortOptions.Email => "Email",
            _ => "PersonId"
        };

        return SqlFilterBuilder.AppendPkTiebreaker($"{column} {direction}", "PersonId");
    }

    public async Task<bool> Refresh(Identity model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var row = await store.QueryAsync(connection => connection.QuerySingleOrDefaultAsync<IdentityRow>(
            "SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive FROM Person WHERE PersonId=$id",
            new { id = model.Id }), cancellationToken);

        if (row is null)
            return false;

        model.FirstName = row.FirstName;
        model.LastName = row.LastName;
        model.PhoneNumber = row.PhoneNumber;
        model.Email = row.Email;
        model.IsActive = row.IsActive == 1;
        return true;
    }

    public Task Save(Identity model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var exists = model.Id > 0 && await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Person WHERE PersonId=$id)", new { id = model.Id }, transaction) == 1;

            if (exists && saveOptions == SaveOptions.Create)
                throw new ModelExistsException();
            if (!exists && saveOptions == SaveOptions.Replace)
                throw new ModelDoesNotExistException();

            if (exists)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE Person SET FirstName=$firstName, LastName=$lastName, PhoneNumber=$phoneNumber,
                                       Email=$email, IsActive=$isActive
                    WHERE PersonId=$id
                    """,
                    new
                    {
                        id = model.Id, firstName = model.FirstName, lastName = model.LastName,
                        phoneNumber = model.PhoneNumber, email = model.Email, isActive = model.IsActive ? 1 : 0
                    }, transaction);
            }
            else
            {
                var id = await connection.ExecuteScalarAsync<long>(
                    """
                    INSERT INTO Person(FirstName, LastName, PhoneNumber, Email, IsActive)
                    VALUES ($firstName, $lastName, $phoneNumber, $email, $isActive);
                    SELECT last_insert_rowid();
                    """,
                    new
                    {
                        firstName = model.FirstName, lastName = model.LastName, phoneNumber = model.PhoneNumber,
                        email = model.Email, isActive = model.IsActive ? 1 : 0
                    }, transaction);
                model.Id = (int)id;
            }
        }, cancellationToken);
    }

    public Task Delete(Identity model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM Person WHERE PersonId=$id", new { id = model.Id }, transaction),
            cancellationToken);
    }
}
