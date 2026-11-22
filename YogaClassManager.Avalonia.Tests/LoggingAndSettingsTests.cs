using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Styling;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.Services.Database;
using YogaClassManager.Avalonia.ViewModels.Settings;
using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     The error log (Services/ErrorLog.cs) and the Settings page's Logs and Appearance sections.
///     ErrorLog is static, so every test that turns it on lives in this one class (xUnit runs a class's
///     tests one at a time) and turns it back off afterwards.
/// </summary>
public class LoggingAndSettingsTests : IDisposable
{
    private readonly string logFolder =
        Path.Combine(Path.GetTempPath(), "ycm-log-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        ErrorLog.Disable();
        try { Directory.Delete(logFolder, recursive: true); } catch { /* best effort */ }
    }

    private string LogText() => File.Exists(ErrorLog.CurrentLogFile) ? File.ReadAllText(ErrorLog.CurrentLogFile!) : "";

    // ------------------------------------------------------------------ ErrorLog

    [Fact]
    public void Error_WritesATimestampedEntry_WithTheFullException()
    {
        ErrorLog.Configure(logFolder);

        try
        {
            throw new InvalidOperationException("database is locked");
        }
        catch (Exception exception)
        {
            ErrorLog.Error("Couldn't load the students", exception);
        }

        var text = LogText();
        Assert.Contains("[ERROR] Couldn't load the students", text);
        Assert.Contains("System.InvalidOperationException: database is locked", text);
        Assert.Contains(nameof(Error_WritesATimestampedEntry_WithTheFullException), text); // the stack trace
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", text);
    }

    [Fact]
    public void Error_WritesNothing_WhileLoggingIsOff()
    {
        ErrorLog.Disable();
        ErrorLog.Error("should not be written");

        Assert.Null(ErrorLog.CurrentLogFile);
        Assert.False(Directory.Exists(logFolder));
    }

    [Fact]
    public void Configure_DeletesLogsOlderThanTheRetentionPeriod()
    {
        Directory.CreateDirectory(logFolder);
        var old = Path.Combine(logFolder, "errors-2020-01-01.log");
        var recent = Path.Combine(logFolder, "errors-recent.log");
        File.WriteAllText(old, "old");
        File.WriteAllText(recent, "recent");
        File.SetLastWriteTime(old, DateTime.Now.AddDays(-(ErrorLog.RetentionDays + 5)));

        ErrorLog.Configure(logFolder);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void AnErrorToast_IsLogged()
    {
        ErrorLog.Configure(logFolder);

        new ToastService().ShowError("Couldn't save the roll: disk full");

        Assert.Contains("Shown to user: Couldn't save the roll: disk full", LogText());
    }

    // ------------------------------------------------------------------ Settings page

    private sealed class InMemorySettings : IAppSettingsService
    {
        public AppSettings Saved { get; private set; } = new() { DatabaseFilePath = @"C:\data\yoga.db" };
        public AppSettings Load() => new() { DatabaseFilePath = Saved.DatabaseFilePath, Theme = Saved.Theme };
        public void Save(AppSettings settings) => Saved = settings;
    }

    private sealed class StubDataStoreProvider : IAppDataStoreProvider
    {
        public string? CurrentFilePath => @"C:\data\yoga.db";
#pragma warning disable CS0067 // required by the interface; nothing here swaps databases
        public event EventHandler? DatabaseSwapped;
#pragma warning restore CS0067
        public Task InitializeAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ProvisionResult> SwapToAsync(string path, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public SqliteDataStore RetrieveDataStore() => throw new NotSupportedException();
    }

    private sealed class StubFilePicker : IFilePickerService
    {
        public Task<string?> PickExistingDatabaseFileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
        public Task<string?> PickNewDatabaseFileLocationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class RecordingFolderLauncher(bool succeeds) : IFolderLauncher
    {
        public List<string> Opened { get; } = [];
        public bool Open(string folder)
        {
            Opened.Add(folder);
            return succeeds;
        }
    }

    private sealed record Fixture(SettingsViewModel ViewModel, InMemorySettings Settings, FakeToastService Toasts,
        RecordingFolderLauncher Launcher, List<ThemeVariant> AppliedThemes);

    private static Fixture CreateSettingsPage(bool launcherSucceeds = true)
    {
        var settings = new InMemorySettings();
        var toasts = new FakeToastService();
        var launcher = new RecordingFolderLauncher(launcherSucceeds);
        var applied = new List<ThemeVariant>();
        var viewModel = new SettingsViewModel(new AppScreen(), settings, new StubDataStoreProvider(),
            new StubFilePicker(), new FakeRollWindowService(), new ScriptedDialogService(), toasts, launcher,
            new ThemeService(settings, applied.Add));
        return new Fixture(viewModel, settings, toasts, launcher, applied);
    }

    [Fact]
    public async Task OpenLogFolder_OpensTheLogDirectory()
    {
        ErrorLog.Configure(logFolder);
        var page = CreateSettingsPage();

        Assert.Equal(logFolder, page.ViewModel.LogDirectory);
        await page.ViewModel.OpenLogFolderCommand.Execute();

        Assert.Equal([logFolder], page.Launcher.Opened);
        Assert.Empty(page.Toasts.Errors);
    }

    [Fact]
    public async Task OpenLogFolder_SaysWhereTheFolderIs_WhenItCantBeOpened()
    {
        ErrorLog.Configure(logFolder);
        var page = CreateSettingsPage(launcherSucceeds: false);

        await page.ViewModel.OpenLogFolderCommand.Execute();

        Assert.Contains(logFolder, Assert.Single(page.Toasts.Errors));
    }

    [Fact]
    public void ChoosingATheme_AppliesItAndSavesIt_KeepingTheOtherSettings()
    {
        var page = CreateSettingsPage();
        Assert.Equal(AppTheme.System, page.ViewModel.SelectedTheme);

        page.ViewModel.SelectedTheme = AppTheme.Dark;

        Assert.Equal([ThemeVariant.Dark], page.AppliedThemes);
        Assert.Equal(AppTheme.Dark, page.Settings.Saved.Theme);
        Assert.Equal(@"C:\data\yoga.db", page.Settings.Saved.DatabaseFilePath); // not wiped by the save
    }

    [Theory]
    [InlineData(AppTheme.System, "Default")]
    [InlineData(AppTheme.Light, "Light")]
    [InlineData(AppTheme.Dark, "Dark")]
    public void ApplySaved_AppliesTheSavedTheme(AppTheme saved, string expectedVariant)
    {
        var settings = new InMemorySettings();
        settings.Save(new AppSettings { Theme = saved });
        var applied = new List<ThemeVariant>();

        new ThemeService(settings, applied.Add).ApplySaved();

        Assert.Equal(expectedVariant, Assert.Single(applied).Key.ToString());
    }

    [Fact]
    public void TheThemeIsStoredByName_AndOldSettingsFilesMatchTheSystem()
    {
        var json = JsonSerializer.Serialize(new AppSettings { Theme = AppTheme.Light });
        Assert.Contains("\"Theme\":\"Light\"", json);

        var fromOlderFile = JsonSerializer.Deserialize<AppSettings>("{\"DatabaseFilePath\":\"x.db\"}")!;
        Assert.Equal(AppTheme.System, fromOlderFile.Theme);
    }
}
