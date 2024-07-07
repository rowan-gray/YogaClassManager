using System.Collections.ObjectModel;
using Dapper;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.People;
using YogaClassManager.NewModels;
using YogaClassManager.NewModels.People;
using Person = YogaClassManager.NewModels.People.Person;

namespace YogaClassManager.NewDatabase.People;

public class PersonDbModel : IDbModel<Person, PersonFilter>
{
    public PersonDbModel(PersonFilter filter, DatabaseService dbService)
    {
        Filter = filter;
        DbService = dbService;
    }

    public PersonFilter Filter { get; init; }
    private DatabaseService DbService { get; init; }

    private string ConstructSelectStatement()
    {
        var defaultSelectStatement = $"SELECT PersonId Id, FirstName, LastName, PhoneNumber, Email, IsActive FROM Person WHERE TRUE";

        if (Filter.Id is not null)
        {
            defaultSelectStatement += $" AND PersonId = {Filter.Id.Value}";
        }

        if (Filter.EmailFilter is not null)
        {
            defaultSelectStatement += $" AND (lower(Email) LIKE lower('{Filter.EmailFilter}%')";
        }

        if (Filter.FirstNameFilter is not null)
        {
            defaultSelectStatement += $" AND (lower(FirstName) LIKE lower('{Filter.FirstNameFilter}%')";
        }

        if (Filter.LastNameFilter is not null)
        {
            defaultSelectStatement += $" AND (lower(LastName) LIKE lower('{Filter.LastNameFilter}%')";
        }

        if (Filter.PhoneNumberFilter is not null)
        {
            defaultSelectStatement += $" AND (lower(PhoneNumberFilter) LIKE lower('{Filter.PhoneNumberFilter}%')";
        }

        if (Filter.IsActive is not null)
        {
            defaultSelectStatement += $" AND IsActive = {Filter.IsActive.Value}";
        }

        if (Filter.SortBy is not null)
        {
            defaultSelectStatement +=
                $" ORDER BY {StringValueAttribute.GetStringValue(Filter.SortBy.Value.Key)} " +
                $"{StringValueAttribute.GetStringValue(Filter.SortBy.Value.Value)}";
        }

        return defaultSelectStatement;
    }
    
    public Task<Person?> LoadSingle(CancellationToken cancellationToken = new())
    {
        return DbService.ExecuteDbFunction<Person?>(async connection => await connection.QuerySingleOrDefaultAsync<Person>(
            new CommandDefinition(ConstructSelectStatement(), null, cancellationToken: cancellationToken)));
    }

    public Task<IEnumerable<Person>> LoadMultiple(uint count = UInt32.MaxValue, uint skip = 0, CancellationToken cancellationToken = new())
    { 
        return DbService.ExecuteDbFunction<IEnumerable<Person>>(async connection => await connection.QueryAsync<Person>(
        new CommandDefinition(ConstructSelectStatement() + $" LIMIT {count} OFFSET {skip}", cancellationToken: cancellationToken)));
    }

    public bool Refresh(Person model, CancellationToken cancellationToken = new())
    {
        throw new NotImplementedException();
    }

    public void Save(Person model, SaveOptions saveOptions = SaveOptions.CreateOrReplace, CancellationToken cancellationToken = new())
    {
        throw new NotImplementedException();
    }
}

public enum PersonSortOptions
{
    [StringValue("PersonId")]
    Id,
    [StringValue("FirstName")]
    FirstName,
    [StringValue("LastName")]
    LastName,
    [StringValue("PhoneNumber")]
    PhoneNumber,
    [StringValue("Email")]
    Email
}

public struct PersonFilter
{
    public uint? Id { get; set; }
    public KeyValuePair<PersonSortOptions, Order>? SortBy { get; set; }
    public string? FirstNameFilter { get; set; }
    public string? LastNameFilter { get; set; }
    public bool? IsActive { get; set; }
    public string? EmailFilter { get; set; }
    public string? PhoneNumberFilter { get; set; }
}