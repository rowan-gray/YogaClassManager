using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Tests.SqliteSpecificTests;

/// <summary>
///     Microsoft.Data.Sqlite's "async" APIs complete synchronously, so a store that just awaited them
///     ran every query on the caller's thread - from the UI, that froze the window (no loading
///     indicator could paint) for the whole query. The store must hand the work to the thread pool.
/// </summary>
public class StoreThreadingTests
{
    [Fact]
    public async Task QueryAsync_ReturnsToTheCaller_WhileTheQueryIsStillRunning()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();
        using var release = new ManualResetEventSlim();

        // A query that blocks until released: run inline on the caller, QueryAsync could not return
        // before Set() - and nothing would ever call Set().
        var query = store.QueryAsync(_ =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return Task.FromResult(42);
        });

        Assert.False(query.IsCompleted);

        release.Set();
        Assert.Equal(42, await query);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ReturnsToTheCaller_WhileTheWorkIsStillRunning()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();
        using var release = new ManualResetEventSlim();

        var write = store.ExecuteInTransactionAsync((_, _) =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return Task.FromResult(7);
        });

        Assert.False(write.IsCompleted);

        release.Set();
        Assert.Equal(7, await write);
    }
}
