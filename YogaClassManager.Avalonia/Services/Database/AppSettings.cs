using System.Text.Json.Serialization;

namespace YogaClassManager.Avalonia.Services.Database;

/// <summary>Persisted app-wide settings - currently just the configured database file path.</summary>
public sealed class AppSettings
{
    public string? DatabaseFilePath { get; set; }

    /// <summary>Settings page "Appearance" choice; stored by name ("System"/"Light"/"Dark") so the
    /// file stays readable. Absent in older settings files, which therefore match the system.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppTheme Theme { get; set; } = AppTheme.System;
}
