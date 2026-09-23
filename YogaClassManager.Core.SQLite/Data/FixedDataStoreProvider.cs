namespace YogaClassManager.Core.SQLite.Data;

/// <summary>
///     The simplest possible <see cref="IDataStoreProvider" />: always returns the same store. For
///     tests, and any consumer that never needs to swap databases at runtime, so they aren't forced to
///     build a real swap-capable provider just to satisfy the repositories' constructor dependency.
/// </summary>
public sealed class FixedDataStoreProvider : IDataStoreProvider
{
    private readonly SqliteDataStore store;

    public FixedDataStoreProvider(SqliteDataStore store)
    {
        this.store = store;
    }

    public SqliteDataStore RetrieveDataStore()
    {
        return store;
    }
}
