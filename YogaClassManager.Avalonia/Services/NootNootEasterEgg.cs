namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     The Dashboard banner's easter egg, carried over from the MAUI app: clicking the banner plays a
///     noot noot. Sounds are the <c>noot*.mp3</c> files copied to <c>Assets/Sounds</c> beside the app
///     (copied as files, not embedded, because the platform players need a real path), handed out by a
///     <see cref="ShuffleBag{T}" /> so every one plays before any repeats and never twice in a row.
///     <para>
///         The files are loudness-normalised copies of the MAUI app's originals (which are left
///         untouched in YogaClassManager/Resources/Raw): one constant gain each, bringing every file to
///         about -18 dBFS RMS with peaks capped at -1 dBFS, re-encoded at its original bitrate. The
///         originals ranged from -25 to -9 dBFS, so some noots were three times louder than others. A
///         new sound added here should be normalised the same way.
///     </para>
/// </summary>
public sealed class NootNootEasterEgg
{
    private readonly ISoundPlayer player;
    private readonly ShuffleBag<string>? sounds;

    public NootNootEasterEgg(ISoundPlayer player, string? soundsDirectory = null, Random? random = null)
    {
        this.player = player;

        var directory = soundsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "noot*.mp3").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
            : [];

        // No sounds shipped (e.g. a trimmed build): the banner just does nothing when clicked.
        if (files.Count > 0)
            sounds = new ShuffleBag<string>(files, random);
    }

    public void PlayNext()
    {
        if (sounds is not null)
            player.Play(sounds.Next());
    }
}
