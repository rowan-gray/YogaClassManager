using System.Reactive;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Core.Dummy;

namespace YogaClassManager.Avalonia.ViewModels.Settings;

/// <summary>
///     Dummy-data-phase settings: a "reset dummy data" action stands in for the MAUI app's DB-file
///     picker, since there's no real database to point at yet.
/// </summary>
public class SettingsViewModel : ViewModelBase, IRoutableViewModel
{
    private readonly InMemoryDataStore store;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;

    public SettingsViewModel(IScreen hostScreen, InMemoryDataStore store, IDialogService dialogService,
        IToastService toastService)
    {
        HostScreen = hostScreen;
        this.store = store;
        this.dialogService = dialogService;
        this.toastService = toastService;

        ResetDummyDataCommand = ReactiveCommand.CreateFromTask(ResetDummyDataAsync);
    }

    public string UrlPathSegment => "settings";
    public IScreen HostScreen { get; }

    public ReactiveCommand<Unit, Unit> ResetDummyDataCommand { get; }

    private async Task ResetDummyDataAsync()
    {
        if (!await dialogService.ConfirmAsync("Reset all data?",
                "Every identity, student, pass, class, roll, and term will be discarded and replaced "
                + "with the original sample data. This can't be undone.", confirmLabel: "Reset"))
            return;

        store.Reset();
        DummyDataSeeder.Seed(store);
        toastService.ShowInfo("Dummy data has been reset. Navigate to another page to see the fresh data.");
    }
}
