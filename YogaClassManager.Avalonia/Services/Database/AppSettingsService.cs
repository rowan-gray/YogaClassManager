using System.Text.Json;

namespace YogaClassManager.Avalonia.Services.Database;

public sealed class AppSettingsService : IAppSettingsService
{
    private static string SettingsDirectory =>
        Environment.GetEnvironmentVariable("YCM_SETTINGS_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YogaClassManager");

    private static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // A missing or corrupt settings file is equivalent to "first launch" - never let a bad
            // settings.json crash startup.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
    }
}
