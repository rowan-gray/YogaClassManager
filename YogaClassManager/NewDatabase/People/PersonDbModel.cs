using YogaClassManager.NewModels;
using YogaClassManager.NewModels.People;

namespace YogaClassManager.NewDatabase.People;

public class PersonDbModel : IDbModel<Person, PersonFilter>
{
    public static Task<Person?> LoadSingle(DatabaseService dbService, PersonFilter filter = default)
    {
        throw new NotImplementedException();
    }

    public static Task<IEnumerable<Person>> LoadMultiple(DatabaseService dbService, PersonFilter filter = default, 
        uint count = UInt32.MaxValue, uint skip = 0)
    {
        throw new NotImplementedException();
    }

    public static bool Refresh(DatabaseService dbService, Person model)
    {
        throw new NotImplementedException();
    }

    public static void Save(DatabaseService dbService, Person model, SaveOptions saveOptions = SaveOptions.CreateOrReplace)
    {
        throw new NotImplementedException();
    }
}

public struct PersonFilter
{
    public KeyValuePair<string, Order>? SortBy { get; set; }
    public string? FirstNameFilter { get; set; }
    public string? LastNameFilter { get; set; }
    public bool? IsActive { get; set; }
    public string? EmailFilter { get; set; }
    public string? PhoneNumberFilter { get; set; }
}