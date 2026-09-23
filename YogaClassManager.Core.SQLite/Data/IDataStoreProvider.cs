namespace YogaClassManager.Core.SQLite.Data;

/// <summary>
///     The single indirection point a repository needs to support its backing store being swapped out
///     at runtime (e.g. an app-level "change database file" feature): repositories take this in their
///     constructor and call <see cref="RetrieveDataStore" /> fresh at the start of every operation,
///     never caching the returned <see cref="SqliteDataStore" /> in a field - the swap then happens in
///     exactly one place, whichever concrete provider is registered, with no changes needed anywhere
///     else.
/// </summary>
public interface IDataStoreProvider
{
    SqliteDataStore RetrieveDataStore();
}
