namespace YogaClassManager.Avalonia.Services.Database;

/// <summary>Persisted app-wide settings - currently just the configured database file path.</summary>
public sealed class AppSettings
{
    public string? DatabaseFilePath { get; set; }
}
