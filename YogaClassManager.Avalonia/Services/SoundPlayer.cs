using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YogaClassManager.Avalonia.Services;

/// <summary>Plays a short audio file, fire-and-forget. Only used for the Dashboard banner's easter
/// egg, so failures are swallowed rather than reported - a missing codec must never break the app.</summary>
public interface ISoundPlayer
{
    void Play(string filePath);
}

/// <summary>
///     Plays MP3s without adding an audio package (Avalonia has no audio API): Windows' built-in MCI
///     player (winmm.dll), macOS's <c>afplay</c>, and on Linux the first of <c>ffplay</c>/<c>mpg123</c>
///     that's installed. A new sound stops the previous one rather than talking over it.
/// </summary>
public sealed class SystemSoundPlayer : ISoundPlayer
{
    private const string MciAlias = "yogaclassmanager_easteregg";
    private Process? current;

    public void Play(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                PlayWithMci(filePath);
            else if (OperatingSystem.IsMacOS())
                PlayWithProcess("afplay", $"\"{filePath}\"");
            else
                _ = PlayWithProcess("ffplay", $"-nodisp -autoexit -loglevel quiet \"{filePath}\"")
                    || PlayWithProcess("mpg123", $"-q \"{filePath}\"");
        }
        catch
        {
            // Easter egg: no sound is better than an error.
        }
    }

    private static void PlayWithMci(string filePath)
    {
        // Close the previous sound (if any) so a new click restarts rather than overlapping.
        MciSendString($"close {MciAlias}", null, 0, IntPtr.Zero);
        if (MciSendString($"open \"{filePath}\" type mpegvideo alias {MciAlias}", null, 0, IntPtr.Zero) == 0)
            MciSendString($"play {MciAlias}", null, 0, IntPtr.Zero);
    }

    private bool PlayWithProcess(string program, string arguments)
    {
        try
        {
            if (current is { HasExited: false })
                current.Kill();
        }
        catch
        {
            // Already gone.
        }

        try
        {
            current = Process.Start(new ProcessStartInfo(program, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            return current is not null;
        }
        catch
        {
            return false; // not installed - let the caller try the next player
        }
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
    private static extern int MciSendString(string command, StringBuilder? returnValue, int returnLength,
        IntPtr callback);
}
