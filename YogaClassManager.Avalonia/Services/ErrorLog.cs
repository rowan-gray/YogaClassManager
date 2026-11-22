using System.Text;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     Writes errors to a daily log file, <c>errors-yyyy-MM-dd.log</c> in <see cref="Directory" />
///     (the app's data folder, <c>%AppData%\YogaClassManager\Logs</c> by default - the same root as
///     settings.json). Each entry is a timestamp, a message and, when there is one, the full exception
///     with its stack trace. The Settings page shows the folder and opens it.
///     <para>
///         Static rather than injected so every layer (base view models, the toast service, global
///         crash handlers) can log without a new constructor parameter on every view model.
///         <b>Off until <see cref="Configure" /> is called</b> - the app does at startup; tests don't
///         (or point it at a temp folder), so a test run never writes into the user's real log.
///     </para>
///     <para>
///         Logging must never cause a second failure: every I/O error is swallowed. Logs older than
///         <see cref="RetentionDays" /> days are deleted the first time the log is configured.
///     </para>
/// </summary>
public static class ErrorLog
{
    public const int RetentionDays = 30;

    private static readonly object Gate = new();
    private static string? directory;

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YogaClassManager",
            "Logs");

    /// <summary>Where logs are written, or null while logging is off.</summary>
    public static string? Directory => directory;

    public static void Configure(string logDirectory)
    {
        lock (Gate)
        {
            directory = logDirectory;
            try
            {
                System.IO.Directory.CreateDirectory(logDirectory);
                DeleteOldLogs(logDirectory);
            }
            catch
            {
                // Unwritable location: the individual writes below will quietly fail too.
            }
        }
    }

    /// <summary>Turns logging back off (tests).</summary>
    public static void Disable()
    {
        lock (Gate)
            directory = null;
    }

    /// <summary>The file today's entries go to (null while logging is off).</summary>
    public static string? CurrentLogFile =>
        directory is null ? null : Path.Combine(directory, $"errors-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Error(string message, Exception? exception = null)
    {
        var entry = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
            .Append(" [ERROR] ")
            .AppendLine(message);
        if (exception is not null)
            entry.AppendLine(exception.ToString());
        entry.AppendLine();

        lock (Gate)
        {
            if (CurrentLogFile is not { } file)
                return;

            try
            {
                File.AppendAllText(file, entry.ToString());
            }
            catch
            {
                // Never let logging take the app down with it.
            }
        }
    }

    private static void DeleteOldLogs(string logDirectory)
    {
        var cutoff = DateTime.Now.AddDays(-RetentionDays);
        foreach (var file in System.IO.Directory.GetFiles(logDirectory, "errors-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
            catch
            {
                // In use or not ours to delete - leave it.
            }
        }
    }
}
