using System.Reactive;
using ReactiveUI;
using Splat;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.Services.Database;
using YogaClassManager.Avalonia.ViewModels.Dashboard;

namespace YogaClassManager.Avalonia.ViewModels.Settings;

public class SettingsViewModel : ViewModelBase, IRoutableViewModel
{
    private readonly IAppSettingsService settingsService;
    private readonly IAppDataStoreProvider dataStoreProvider;
    private readonly IFilePickerService filePickerService;
    private readonly IRollWindowService rollWindowService;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;

    private string? currentDatabasePath;

    public SettingsViewModel(IScreen hostScreen, IAppSettingsService settingsService,
        IAppDataStoreProvider dataStoreProvider, IFilePickerService filePickerService,
        IRollWindowService rollWindowService, IDialogService dialogService, IToastService toastService)
    {
        HostScreen = hostScreen;
        this.settingsService = settingsService;
        this.dataStoreProvider = dataStoreProvider;
        this.filePickerService = filePickerService;
        this.rollWindowService = rollWindowService;
        this.dialogService = dialogService;
        this.toastService = toastService;

        currentDatabasePath = dataStoreProvider.CurrentFilePath;
        dataStoreProvider.DatabaseSwapped += (_, _) => CurrentDatabasePath = dataStoreProvider.CurrentFilePath;

        BrowseExistingDatabaseCommand =
            ReactiveCommand.CreateFromTask(() => ChangeDatabaseAsync(filePickerService.PickExistingDatabaseFileAsync));
        CreateNewDatabaseCommand =
            ReactiveCommand.CreateFromTask(() => ChangeDatabaseAsync(filePickerService.PickNewDatabaseFileLocationAsync));
    }

    public string UrlPathSegment => "settings";
    public IScreen HostScreen { get; }

    public string? CurrentDatabasePath
    {
        get => currentDatabasePath;
        private set => this.RaiseAndSetIfChanged(ref currentDatabasePath, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseExistingDatabaseCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateNewDatabaseCommand { get; }

    private async Task ChangeDatabaseAsync(Func<CancellationToken, Task<string?>> pickPath)
    {
        var path = await pickPath(CancellationToken.None);
        if (path is null)
            return;

        if (!await dialogService.ConfirmAsync("Change database?",
                "The app will immediately start using the selected database. Any in-progress roll marking "
                + "will be closed. This can't be undone.", confirmLabel: "Change"))
            return;

        StartBusy();
        try
        {
            rollWindowService.CloseAll();

            var result = await dataStoreProvider.SwapToAsync(path);
            if (!result.Success)
            {
                toastService.ShowError(result.ErrorMessage!);
                return;
            }

            settingsService.Save(new AppSettings { DatabaseFilePath = path });
            CurrentDatabasePath = path;
            toastService.ShowInfo(result.BackupPath is { } backup
                ? $"Database changed. A backup of the previous file was saved as {Path.GetFileName(backup)}."
                : "Database changed.");

            // Fresh instance (page ViewModels are transient) bound to the now-swapped repositories -
            // moves the visible page away from Settings back to Dashboard so the user immediately sees
            // live data from the new database rather than continuing to look at the no-longer-relevant
            // Settings page.
            HostScreen.Router.Navigate.Execute(Locator.Current.GetService<DashboardViewModel>()!).Subscribe();
        }
        finally
        {
            EndBusy();
        }
    }
}
