using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace YogaClassManager.Avalonia.Services.Database;

public sealed class FilePickerService : IFilePickerService
{
    private static readonly FilePickerFileType DatabaseFileType = new("Database files") { Patterns = ["*.db"] };

    /// <summary>Always the currently-active top-level window (the startup gate window before
    /// hand-off, the real MainWindow afterwards) - resolved fresh per call rather than threading a
    /// Visual/control reference through any ViewModel.</summary>
    private static IStorageProvider StorageProvider =>
        ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!.StorageProvider;

    public async Task<string?> PickExistingDatabaseFileAsync(CancellationToken cancellationToken = default)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a database file",
            AllowMultiple = false,
            FileTypeFilter = [DatabaseFileType, FilePickerFileTypes.All]
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickNewDatabaseFileLocationAsync(CancellationToken cancellationToken = default)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Create a new database file",
            SuggestedFileName = "yogamanager.db",
            DefaultExtension = "db",
            FileTypeChoices = [DatabaseFileType]
        });

        return file?.TryGetLocalPath();
    }
}
