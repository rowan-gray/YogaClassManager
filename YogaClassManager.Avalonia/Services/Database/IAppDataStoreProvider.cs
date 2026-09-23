using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Avalonia.Services.Database;

/// <summary>The one concrete swap point in the whole app: extends Core.SQLite's IDataStoreProvider
/// (which every SqliteXxxRepository takes in its constructor and calls RetrieveDataStore() on fresh
/// per operation, never caching it) with the app-level bind/swap/settings surface. Because every
/// repository resolves the store fresh from here rather than caching it, swapping is just reassigning
/// one field on the concrete implementation - no repository object identity ever changes, so
/// already-injected ViewModel references transparently observe a swap on their next call.</summary>
public interface IAppDataStoreProvider : IDataStoreProvider
{
    string? CurrentFilePath { get; }

    event EventHandler? DatabaseSwapped;

    /// <summary>First bind - throws on failure (used only for the very first bind, where failure means
    /// "stay on the startup gate").</summary>
    Task InitializeAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Hot-swap to a different database while the app is already running - never throws,
    /// callers inspect ProvisionResult.Success instead.</summary>
    Task<ProvisionResult> SwapToAsync(string path, CancellationToken cancellationToken = default);
}
