using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Avalonia.Services.Database;

public sealed class AppDataStoreProvider : IAppDataStoreProvider
{
    private readonly IDatabaseProvisioningService provisioning;
    private SqliteDataStore? store;

    public AppDataStoreProvider(IDatabaseProvisioningService provisioning)
    {
        this.provisioning = provisioning;
    }

    public string? CurrentFilePath { get; private set; }

    public event EventHandler? DatabaseSwapped;

    public SqliteDataStore RetrieveDataStore()
    {
        return store ?? throw new InvalidOperationException("No database has been opened yet.");
    }

    public async Task InitializeAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await provisioning.ProvisionAsync(path, cancellationToken);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage);

        store = result.Store;
        CurrentFilePath = path;
    }

    public async Task<ProvisionResult> SwapToAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await provisioning.ProvisionAsync(path, cancellationToken);
        if (!result.Success)
            return result;

        var old = store;
        store = result.Store;
        CurrentFilePath = path;

        if (old is not null)
            await old.DisposeAsync();

        DatabaseSwapped?.Invoke(this, EventArgs.Empty);
        return result;
    }
}
