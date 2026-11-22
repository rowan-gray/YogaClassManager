using Microsoft.Data.Sqlite;
using YogaClassManager.Core.SQLite.Schema;

namespace YogaClassManager.Core.SQLite.Data;

/// <summary>
///     Owns a single shared <see cref="SqliteConnection" /> for the lifetime of the store, mirroring
///     the old MAUI DatabaseManager's single-connection-per-app-lifetime approach. All connection
///     access - reads included, not just writes - is serialized through one gate: SqliteConnection is
///     not safe for concurrent use by multiple threads regardless of WAL mode (WAL enables concurrency
///     between separate connections, not within one shared instance).
///     <para>
///         <b>Every operation runs on the thread pool, not the caller's thread.</b> Microsoft.Data.Sqlite's
///         "async" APIs actually complete synchronously, so awaiting them directly from the UI thread
///         froze the window for the whole query - navigating to a page hung with no way to paint its
///         loading indicator. <c>Task.Run</c> moves the work (still serialized by the gate) off the
///         caller's thread; callers keep awaiting normally and resume on their own context. Never block
///         on a store call (<c>.Result</c>, <c>.Wait()</c>, <c>GetAwaiter().GetResult()</c>) from the UI
///         thread: the store resumes there to release its gate, so that would deadlock.
///     </para>
/// </summary>
public sealed class SqliteDataStore : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteDataStore(SqliteConnection connection)
    {
        this.connection = connection;
    }

    /// <summary>Opens and migrates on the thread pool too - migrating a large legacy database can take
    /// a while, and the startup screen should keep painting meanwhile.</summary>
    public static Task<SqliteDataStore> OpenAsync(string dataSource, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            SqliteTypeHandlers.EnsureRegistered();

            var connection = new SqliteConnection(dataSource);
            await connection.OpenAsync(cancellationToken);

            await using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
                await pragma.ExecuteNonQueryAsync(cancellationToken);
            }

            await SchemaMigrator.EnsureUpToDateAsync(connection, cancellationToken);

            return new SqliteDataStore(connection);
        }, cancellationToken);
    }

    /// <summary>Runs a read-only operation against the shared connection, serialized through the same
    /// gate writes use (see class remarks for why reads must be gated too, not just writes).</summary>
    public async Task<T> QueryAsync<T>(Func<SqliteConnection, Task<T>> query, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Off the caller's thread - see class remarks.
            return await Task.Run(() => query(connection), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Runs a write (or read+write) operation inside an explicit transaction, serialized
    /// through the same gate reads use. All multi-statement repository writes (Add/Update/Delete,
    /// Merge, etc.) go through this - never issue a bare write directly against a connection obtained
    /// any other way.</summary>
    public Task ExecuteInTransactionAsync(Func<SqliteConnection, SqliteTransaction, Task> action,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync<object?>(async (c, t) =>
        {
            await action(c, t);
            return null;
        }, cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<SqliteConnection, SqliteTransaction, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            // The whole transaction runs off the caller's thread - see class remarks.
            return await Task.Run(async () =>
            {
                var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    var result = await action(connection, transaction);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
                finally
                {
                    await transaction.DisposeAsync();
                }
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
        gate.Dispose();
    }
}
