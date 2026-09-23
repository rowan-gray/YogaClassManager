namespace YogaClassManager.Avalonia.Services.Database;

/// <summary>Loads/persists <see cref="AppSettings" /> to a small JSON file outside the app's install
/// location, so the configured database file survives an app upgrade/reinstall.</summary>
public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
