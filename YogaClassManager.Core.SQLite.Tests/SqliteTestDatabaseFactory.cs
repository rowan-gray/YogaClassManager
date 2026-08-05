using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Tests;

/// <summary>Opens a fresh, uniquely-named in-memory SQLite database (shared-cache so the single
/// connection Microsoft.Data.Sqlite hands back actually sees the schema the migrator just created)
/// and runs the schema migrator against it - the standard per-test isolation unit for every SQLite
/// repository test in this project.</summary>
internal static class SqliteTestDatabaseFactory
{
    public static Task<SqliteDataStore> CreateAsync()
    {
        var name = Guid.NewGuid().ToString("N");
        return SqliteDataStore.OpenAsync($"Data Source=file:{name}?mode=memory&cache=shared");
    }
}
