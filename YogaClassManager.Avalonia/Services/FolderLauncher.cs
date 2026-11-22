using System.Diagnostics;

namespace YogaClassManager.Avalonia.Services;

/// <summary>Opens a folder in the operating system's file manager (the Settings page's "Open log
/// folder"). An interface so the page can be tested without actually launching Explorer.</summary>
public interface IFolderLauncher
{
    /// <summary>Opens <paramref name="folder" />, creating it first if it doesn't exist yet. Returns
    /// false if the file manager couldn't be started.</summary>
    bool Open(string folder);
}

public sealed class FolderLauncher : IFolderLauncher
{
    public bool Open(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);

            var start = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("explorer.exe", $"\"{folder}\"")
                : OperatingSystem.IsMacOS()
                    ? new ProcessStartInfo("open", $"\"{folder}\"")
                    : new ProcessStartInfo("xdg-open", $"\"{folder}\"");
            start.UseShellExecute = false;

            return Process.Start(start) is not null;
        }
        catch (Exception exception)
        {
            ErrorLog.Error($"Couldn't open the folder {folder}", exception);
            return false;
        }
    }
}
