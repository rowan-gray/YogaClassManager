using Dapper;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Repositories;

public class SqliteEmergencyContactRepository : IEmergencyContactRepository
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteEmergencyContactRepository(IDataStoreProvider dataStoreProvider)
    {
        this.dataStoreProvider = dataStoreProvider;
    }

    public IDbModel<EmergencyContact, EmergencyContactFilter> Query(EmergencyContactFilter filter)
    {
        return new SqliteEmergencyContactDbModel(filter, dataStoreProvider);
    }
}

internal sealed class EmergencyContactRow
{
    public int StudentId { get; set; }
    public int PersonId { get; set; }
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int Relationship { get; set; }

    public EmergencyContact ToModel()
    {
        // EmergencyContact's own constructor always hard-codes IsActive=true regardless of the
        // underlying Person row - an existing model quirk being replicated exactly, not fixed.
        return new EmergencyContact(PersonId, FirstName, LastName, PhoneNumber, Email, StudentId,
            (Relationship)Relationship);
    }
}

internal class SqliteEmergencyContactDbModel : IDbModel<EmergencyContact, EmergencyContactFilter>
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteEmergencyContactDbModel(EmergencyContactFilter filter, IDataStoreProvider dataStoreProvider)
    {
        Filter = filter;
        this.dataStoreProvider = dataStoreProvider;
    }

    public EmergencyContactFilter Filter { get; init; }

    public async Task<EmergencyContact?> LoadSingle(CancellationToken cancellationToken = default)
    {
        var results = await LoadMultiple(1, 0, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<EmergencyContact>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var filter = Filter;
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        var orderBy = BuildOrderBy(filter.SortBy);

        var sql = $"""
                   SELECT StudentId, PersonId, FirstName, LastName, PhoneNumber, Email, Relationship
                   FROM EmergencyContactDetails
                   WHERE ($id IS NULL OR PersonId = $id)
                     AND ($studentId IS NULL OR StudentId = $studentId)
                   ORDER BY {orderBy}
                   LIMIT $take OFFSET $skip
                   """;

        var rows = await store.QueryAsync(connection => connection.QueryAsync<EmergencyContactRow>(sql, new
        {
            id = (int?)filter.Id,
            studentId = filter.StudentId,
            take,
            skip = (int)skip
        }), cancellationToken);

        return rows.Select(r => r.ToModel()).ToList();
    }

    private static string BuildOrderBy(KeyValuePair<EmergencyContactSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return SqlFilterBuilder.AppendPkTiebreaker("PersonId ASC", "PersonId");

        var (key, order) = sortBy.Value;
        var direction = StringValueAttribute.GetStringValue(order);

        var column = key switch
        {
            EmergencyContactSortOptions.FirstName => "FirstName",
            EmergencyContactSortOptions.LastName => "LastName",
            _ => "PersonId"
        };

        return SqlFilterBuilder.AppendPkTiebreaker($"{column} {direction}", "PersonId");
    }

    public async Task<bool> Refresh(EmergencyContact model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var exists = await store.QueryAsync(connection => connection.ExecuteScalarAsync<long>(
            "SELECT EXISTS(SELECT 1 FROM StudentEmergencyContacts WHERE StudentId=$studentId AND EmergencyContactId=$id)",
            new { studentId = model.StudentId, id = model.Id }), cancellationToken);

        return exists == 1;
    }

    /// <summary>Functionally identical to IStudentRepository.LinkEmergencyContactAsync's upsert - the
    /// in-memory backend wires both up as separate but equivalent "remove existing link, add fresh
    /// one" paths, and this SQL implementation preserves that same parity (writes for emergency
    /// contacts are documented as normally going through IStudentRepository, but IDbModel.Save/Delete
    /// still need to work functionally here to match the in-memory contract).</summary>
    public Task Save(EmergencyContact model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var exists = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM StudentEmergencyContacts WHERE StudentId=$studentId AND EmergencyContactId=$id)",
                new { studentId = model.StudentId, id = model.Id }, transaction) == 1;

            if (exists && saveOptions == SaveOptions.Create)
                throw new ModelExistsException();
            if (!exists && saveOptions == SaveOptions.Replace)
                throw new ModelDoesNotExistException();

            await connection.ExecuteAsync(
                """
                INSERT INTO StudentEmergencyContacts(StudentId, EmergencyContactId, Relationship)
                VALUES ($studentId, $id, $relationship)
                ON CONFLICT(StudentId, EmergencyContactId) DO UPDATE SET Relationship = excluded.Relationship
                """,
                new { studentId = model.StudentId, id = model.Id, relationship = (int)model.Relationship }, transaction);
        }, cancellationToken);
    }

    public Task Delete(EmergencyContact model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync(
                "DELETE FROM StudentEmergencyContacts WHERE StudentId=$studentId AND EmergencyContactId=$id",
                new { studentId = model.StudentId, id = model.Id }, transaction), cancellationToken);
    }
}
