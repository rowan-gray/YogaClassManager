using Dapper;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Repositories;

public class SqliteStudentRepository : IStudentRepository
{
    private readonly IDataStoreProvider dataStoreProvider;
    private readonly IIdentityRepository identityRepository;
    private readonly IPassRepository passRepository;
    private readonly IEmergencyContactRepository emergencyContactRepository;

    public SqliteStudentRepository(IDataStoreProvider dataStoreProvider, IIdentityRepository identityRepository,
        IPassRepository passRepository, IEmergencyContactRepository emergencyContactRepository)
    {
        this.dataStoreProvider = dataStoreProvider;
        this.identityRepository = identityRepository;
        this.passRepository = passRepository;
        this.emergencyContactRepository = emergencyContactRepository;
    }

    public IDbModel<Student, StudentFilter> Query(StudentFilter filter)
    {
        return new SqliteStudentDbModel(filter, dataStoreProvider, passRepository, emergencyContactRepository);
    }

    public Task<int> AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var personExists = student.Id > 0 && await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Person WHERE PersonId=$id)", new { id = student.Id }, transaction) == 1;

            if (!personExists)
            {
                var id = await connection.ExecuteScalarAsync<long>(
                    """
                    INSERT INTO Person(FirstName, LastName, PhoneNumber, Email, IsActive)
                    VALUES ($firstName, $lastName, $phoneNumber, $email, $isActive);
                    SELECT last_insert_rowid();
                    """,
                    new
                    {
                        firstName = student.FirstName, lastName = student.LastName, phoneNumber = student.PhoneNumber,
                        email = student.Email, isActive = student.IsActive ? 1 : 0
                    }, transaction);
                student.Id = (int)id;
            }

            await connection.ExecuteAsync("INSERT INTO Student(StudentId) VALUES ($id)", new { id = student.Id },
                transaction);

            student.AttachChildRepositories(passRepository, emergencyContactRepository);
            return student.Id;
        }, cancellationToken);
    }

    public Task UpdateAsync(Student student, CancellationToken cancellationToken = default)
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
                    id = student.Id, firstName = student.FirstName, lastName = student.LastName,
                    phoneNumber = student.PhoneNumber, email = student.Email, isActive = student.IsActive ? 1 : 0
                }, transaction), cancellationToken);
    }

    public Task<int> PromoteToStudentAsync(Student student, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var personExists = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Person WHERE PersonId=$id)", new { id = student.Id }, transaction) == 1;
            if (!personExists)
                throw new InvalidOperationException($"Identity {student.Id} does not exist.");

            var alreadyStudent = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Student WHERE StudentId=$id)", new { id = student.Id }, transaction) == 1;
            if (alreadyStudent)
                throw new InvalidOperationException($"Identity {student.Id} is already a Student.");

            await connection.ExecuteAsync("INSERT INTO Student(StudentId) VALUES ($id)", new { id = student.Id },
                transaction);

            student.AttachChildRepositories(passRepository, emergencyContactRepository);
            return student.Id;
        }, cancellationToken);
    }

    public Task<ArchiveResult> ArchiveOrDeleteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return identityRepository.ArchiveOrDeleteAsync(studentId, cancellationToken);
    }

    public Task UnarchiveAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return identityRepository.UnarchiveAsync(studentId, cancellationToken);
    }

    public Task LinkEmergencyContactAsync(int studentId, int identityId, Relationship relationship,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync(
                """
                INSERT INTO StudentEmergencyContacts(StudentId, EmergencyContactId, Relationship)
                VALUES ($studentId, $identityId, $relationship)
                ON CONFLICT(StudentId, EmergencyContactId) DO UPDATE SET Relationship = excluded.Relationship
                """,
                new { studentId, identityId, relationship = (int)relationship }, transaction), cancellationToken);
    }

    public Task UnlinkEmergencyContactAsync(int studentId, int identityId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM StudentEmergencyContacts WHERE StudentId=$studentId AND EmergencyContactId=$identityId",
                new { studentId, identityId }, transaction), cancellationToken);
    }

    public Task<IReadOnlyList<EmergencyContact>> GetEmergencyContactsAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        return emergencyContactRepository.Query(new EmergencyContactFilter { StudentId = studentId })
            .LoadMultiple(cancellationToken: cancellationToken);
    }

    public Task AddHealthConcernAsync(int studentId, string concern, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("INSERT OR IGNORE INTO StudentHealthConcerns(StudentId, HealthConcern) VALUES ($studentId, $concern)",
                new { studentId, concern }, transaction), cancellationToken);
    }

    public Task RemoveHealthConcernAsync(int studentId, string concern, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM StudentHealthConcerns WHERE StudentId=$studentId AND HealthConcern=$concern",
                new { studentId, concern }, transaction), cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetHealthConcernsAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var concerns = await store.QueryAsync(connection => connection.QueryAsync<string>(
            "SELECT HealthConcern FROM StudentHealthConcerns WHERE StudentId=$studentId", new { studentId }),
            cancellationToken);
        return concerns.ToList();
    }

    public Task<IReadOnlyList<Pass>> GetPassesAsync(int studentId, bool includeExpired = false,
        bool includeDepleted = false, CancellationToken cancellationToken = default)
    {
        return passRepository
            .Query(new PassFilter { StudentId = studentId, IncludeExpired = includeExpired, IncludeDepleted = includeDepleted })
            .LoadMultiple(cancellationToken: cancellationToken);
    }
}

internal sealed class StudentRow
{
    public int PersonId { get; set; }
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int IsActive { get; set; }

    public Student ToModel()
    {
        return new Student(new Identity(PersonId, FirstName, LastName, PhoneNumber, Email, IsActive == 1));
    }
}

internal class SqliteStudentDbModel : IDbModel<Student, StudentFilter>
{
    private readonly IDataStoreProvider dataStoreProvider;
    private readonly IPassRepository passRepository;
    private readonly IEmergencyContactRepository emergencyContactRepository;

    public SqliteStudentDbModel(StudentFilter filter, IDataStoreProvider dataStoreProvider,
        IPassRepository passRepository, IEmergencyContactRepository emergencyContactRepository)
    {
        Filter = filter;
        this.dataStoreProvider = dataStoreProvider;
        this.passRepository = passRepository;
        this.emergencyContactRepository = emergencyContactRepository;
    }

    public StudentFilter Filter { get; init; }

    public async Task<Student?> LoadSingle(CancellationToken cancellationToken = default)
    {
        var results = await LoadMultiple(1, 0, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Student>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var filter = Filter;
        var take = count > int.MaxValue ? int.MaxValue : (int)count;

        var needsAttendanceFilter = filter.LastAttendedFrom is not null || filter.LastAttendedTo is not null;
        var sortsByLastAttendance = filter.SortBy?.Key == StudentSortOptions.LastAttendance;

        // Structural difference (inner vs left join), not a value comparison - built in C#, not
        // parameterized. Inner join when filtering by attendance range (students with no attendance
        // are excluded entirely); left join when only sorting by it (students with no attendance
        // still appear, sorted last - see BuildOrderBy).
        var joinClause = needsAttendanceFilter
            ? "JOIN StudentLastAttendance sla ON sla.StudentId = Person.PersonId"
            : sortsByLastAttendance
                ? "LEFT JOIN StudentLastAttendance sla ON sla.StudentId = Person.PersonId"
                : "";

        var excludeIdsClause = filter.ExcludeIds is { Count: > 0 } ? "AND Person.PersonId NOT IN @excludeIds" : "";

        // sla is only present in the FROM clause when needsAttendanceFilter joined it - referencing
        // sla.LastAttendedDate when it isn't joined is a SQL parse error (a static column reference,
        // not something SQLite short-circuits away even inside an "IS NULL OR" branch).
        var attendanceFilterClause = needsAttendanceFilter
            ? "AND ($lastAttendedFrom IS NULL OR sla.LastAttendedDate >= $lastAttendedFrom) AND ($lastAttendedTo IS NULL OR sla.LastAttendedDate <= $lastAttendedTo)"
            : "";

        const string fullNameExpr = "(CASE WHEN Person.LastName IS NULL THEN Person.FirstName ELSE Person.FirstName || ' ' || Person.LastName END)";

        var orderBy = BuildOrderBy(filter.SortBy);

        var sql = $"""
                   SELECT Person.PersonId AS PersonId, Person.FirstName AS FirstName, Person.LastName AS LastName,
                          Person.PhoneNumber AS PhoneNumber, Person.Email AS Email, Person.IsActive AS IsActive
                   FROM Person
                   JOIN Student ON Student.StudentId = Person.PersonId
                   {joinClause}
                   WHERE ($id IS NULL OR Person.PersonId = $id)
                     AND ($isActive IS NULL OR Person.IsActive = $isActive)
                     {excludeIdsClause}
                     AND ($nameFilter IS NULL OR
                          {fullNameExpr} LIKE $nameContains ESCAPE '\'
                          OR Person.FirstName LIKE $namePrefix ESCAPE '\'
                          OR (Person.LastName IS NOT NULL AND Person.LastName LIKE $namePrefix ESCAPE '\'))
                     {attendanceFilterClause}
                   ORDER BY {orderBy}
                   LIMIT $take OFFSET $skip
                   """;

        var rows = await store.QueryAsync(connection => connection.QueryAsync<StudentRow>(sql, new
        {
            id = (int?)filter.Id,
            isActive = filter.IsActive is null ? null : (int?)(filter.IsActive.Value ? 1 : 0),
            excludeIds = filter.ExcludeIds ?? Array.Empty<int>(),
            nameFilter = filter.NameFilter,
            nameContains = filter.NameFilter is null ? null : SqlFilterBuilder.ContainsPattern(filter.NameFilter),
            namePrefix = filter.NameFilter is null ? null : SqlFilterBuilder.StartsWithPattern(filter.NameFilter),
            lastAttendedFrom = filter.LastAttendedFrom,
            lastAttendedTo = filter.LastAttendedTo,
            take,
            skip = (int)skip
        }), cancellationToken);

        var students = rows.Select(r => r.ToModel()).ToList();
        foreach (var student in students)
            student.AttachChildRepositories(passRepository, emergencyContactRepository);

        return students;
    }

    private static string BuildOrderBy(KeyValuePair<StudentSortOptions, Order>? sortBy)
    {
        // Default differs from every other entity's Id-default: Students default-sort by FirstName.
        if (sortBy is null)
            return SqlFilterBuilder.AppendPkTiebreaker("Person.FirstName ASC", "Person.PersonId");

        var (key, order) = sortBy.Value;
        var direction = StringValueAttribute.GetStringValue(order);

        if (key == StudentSortOptions.LastAttendance)
        {
            // (LastAttendedDate IS NULL) evaluates to 0/1; ordering that ASC always places students
            // with a date (0) before students without one (1), regardless of the second key's
            // direction - reproducing "no-attendance students always sort last regardless of direction".
            return SqlFilterBuilder.AppendPkTiebreaker(
                $"(sla.LastAttendedDate IS NULL) ASC, sla.LastAttendedDate {direction}", "Person.PersonId");
        }

        var column = key switch
        {
            StudentSortOptions.FirstName => "Person.FirstName",
            StudentSortOptions.LastName => "Person.LastName",
            _ => "Person.PersonId"
        };

        return SqlFilterBuilder.AppendPkTiebreaker($"{column} {direction}", "Person.PersonId");
    }

    public async Task<bool> Refresh(Student model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var row = await store.QueryAsync(connection => connection.QuerySingleOrDefaultAsync<StudentRow>(
            """
            SELECT Person.PersonId AS PersonId, Person.FirstName AS FirstName, Person.LastName AS LastName,
                   Person.PhoneNumber AS PhoneNumber, Person.Email AS Email, Person.IsActive AS IsActive
            FROM Person JOIN Student ON Student.StudentId = Person.PersonId
            WHERE Person.PersonId = $id
            """, new { id = model.Id }), cancellationToken);

        if (row is null)
            return false;

        model.FirstName = row.FirstName;
        model.LastName = row.LastName;
        model.PhoneNumber = row.PhoneNumber;
        model.Email = row.Email;
        model.IsActive = row.IsActive == 1;
        return true;
    }

    public Task Save(Student model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var exists = model.Id > 0 && await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Student WHERE StudentId=$id)", new { id = model.Id }, transaction) == 1;

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
                var personExists = model.Id > 0 && await connection.ExecuteScalarAsync<long>(
                    "SELECT EXISTS(SELECT 1 FROM Person WHERE PersonId=$id)", new { id = model.Id }, transaction) == 1;

                if (!personExists)
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

                await connection.ExecuteAsync("INSERT INTO Student(StudentId) VALUES ($id)", new { id = model.Id },
                    transaction);
            }
        }, cancellationToken);
    }

    public Task Delete(Student model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM Person WHERE PersonId=$id", new { id = model.Id }, transaction),
            cancellationToken);
    }
}
