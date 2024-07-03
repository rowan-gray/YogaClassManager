using SQLite;

namespace YogaClassManager.Resources;

public static class AppConstants
{
    public const SQLiteOpenFlags Flags =
        // open the database in read/write mode
        SQLiteOpenFlags.ReadWrite |
        // enable multi-threaded database access
        SQLiteOpenFlags.SharedCache |
        // database file isn't encrypted.
        SQLiteOpenFlags.ProtectionNone;
}