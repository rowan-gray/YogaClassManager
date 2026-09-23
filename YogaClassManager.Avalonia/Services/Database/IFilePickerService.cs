namespace YogaClassManager.Avalonia.Services.Database;

/// <summary>Wraps Avalonia's native file-picker APIs behind a small abstraction, consistent with how
/// IPageNavigator/IDialogService already keep Avalonia-specific concerns out of ViewModels.</summary>
public interface IFilePickerService
{
    /// <summary>Lets the user pick an existing database file. Returns null if the picker was cancelled.</summary>
    Task<string?> PickExistingDatabaseFileAsync(CancellationToken cancellationToken = default);

    /// <summary>Lets the user choose a location/filename for a new database file (doesn't need to exist
    /// yet). Returns null if the picker was cancelled.</summary>
    Task<string?> PickNewDatabaseFileLocationAsync(CancellationToken cancellationToken = default);
}
