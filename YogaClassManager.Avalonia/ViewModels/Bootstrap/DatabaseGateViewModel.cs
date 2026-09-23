using System.Reactive;
using ReactiveUI;
using YogaClassManager.Avalonia.Services.Database;

namespace YogaClassManager.Avalonia.ViewModels.Bootstrap;

public enum GateState
{
    Loading,
    NeedsSelection,
    Completing
}

/// <summary>
///     Backs the startup gate window (Views/Bootstrap/DatabaseGateWindow), which runs before MainWindow
///     exists: resolves the persisted database file if there is one, or lets the user pick/create one,
///     and only ever raises <see cref="Completed" /> once a real store has been successfully bound via
///     IAppDataStoreProvider - App.axaml.cs doesn't construct MainWindow (and therefore doesn't touch
///     any repository) until that happens.
/// </summary>
public sealed class DatabaseGateViewModel : ViewModelBase
{
    private readonly IAppSettingsService settingsService;
    private readonly IAppDataStoreProvider dataStoreProvider;
    private readonly IFilePickerService filePickerService;

    private GateState state = GateState.Loading;
    private string? errorMessage;

    public DatabaseGateViewModel(IAppSettingsService settingsService, IAppDataStoreProvider dataStoreProvider,
        IFilePickerService filePickerService)
    {
        this.settingsService = settingsService;
        this.dataStoreProvider = dataStoreProvider;
        this.filePickerService = filePickerService;

        BrowseExistingCommand = ReactiveCommand.CreateFromTask(BrowseExistingAsync);
        CreateNewCommand = ReactiveCommand.CreateFromTask(CreateNewAsync);
    }

    public GateState State
    {
        get => state;
        private set => this.RaiseAndSetIfChanged(ref state, value);
    }

    /// <summary>Null when there's no error banner to show.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => this.RaiseAndSetIfChanged(ref errorMessage, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseExistingCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateNewCommand { get; }

    /// <summary>Fires exactly once, with the confirmed path, once a store has been bound.</summary>
    public event EventHandler<string>? Completed;

    /// <summary>Entry point - called once, right after the gate window is shown.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        State = GateState.Loading;

        var settings = settingsService.Load();
        if (settings.DatabaseFilePath is { } path)
        {
            if (!File.Exists(path))
            {
                // A missing *configured* file is a hard error, never silently replaced with a blank
                // database at that path - that would look to the user like their data just vanished.
                ErrorMessage = $"The configured database file could not be found:\n{path}";
                State = GateState.NeedsSelection;
                return;
            }

            await TryBindAsync(path, cancellationToken);
            return;
        }

        State = GateState.NeedsSelection;
    }

    private async Task BrowseExistingAsync()
    {
        var path = await filePickerService.PickExistingDatabaseFileAsync();
        if (path is not null)
            await TryBindAsync(path, CancellationToken.None);
    }

    private async Task CreateNewAsync()
    {
        var path = await filePickerService.PickNewDatabaseFileLocationAsync();
        if (path is not null)
            await TryBindAsync(path, CancellationToken.None);
    }

    private async Task TryBindAsync(string path, CancellationToken cancellationToken)
    {
        StartBusy();
        try
        {
            await dataStoreProvider.InitializeAsync(path, cancellationToken);
            settingsService.Save(new AppSettings { DatabaseFilePath = path });
            State = GateState.Completing;
            Completed?.Invoke(this, path);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = GateState.NeedsSelection;
        }
        finally
        {
            EndBusy();
        }
    }
}
