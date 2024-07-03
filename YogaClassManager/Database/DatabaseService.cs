using SQLite;

namespace YogaClassManager.Database;

public class DatabaseService
{
    protected readonly DatabaseManager dbManager;

    public DatabaseService(DatabaseManager dbManager)
    {
        this.dbManager = dbManager;
    }

    protected SQLiteAsyncConnection Database => dbManager.Database;

    protected DateOnly StringToDateOnly(string date)
    {
        return DateOnly.ParseExact(date, "yyyy-MM-dd");
    }
}