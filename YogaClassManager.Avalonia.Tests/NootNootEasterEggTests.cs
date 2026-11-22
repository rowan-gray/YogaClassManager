using YogaClassManager.Avalonia.Services;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>Records what would have been played, instead of making noise in the test run.</summary>
public sealed class FakeSoundPlayer : ISoundPlayer
{
    public List<string> Played { get; } = [];

    public void Play(string filePath) => Played.Add(filePath);
}

/// <summary>The Dashboard banner's noot noot easter egg and the smart shuffle behind it.</summary>
public class NootNootEasterEggTests
{
    [Fact]
    public void ShuffleBag_PlaysEveryItemOncePerRound()
    {
        var bag = new ShuffleBag<int>([1, 2, 3, 4, 5], new Random(1));

        for (var round = 0; round < 20; round++)
        {
            var played = Enumerable.Range(0, 5).Select(_ => bag.Next()).ToList();
            Assert.Equal([1, 2, 3, 4, 5], played.Order());
        }
    }

    [Fact]
    public void ShuffleBag_NeverRepeatsBackToBack_EvenAcrossRounds()
    {
        // Many seeds, many rounds: the boundary case (a new round starting with the previous round's
        // last item) is exactly what plain shuffling gets wrong about 1 time in N.
        for (var seed = 0; seed < 200; seed++)
        {
            var bag = new ShuffleBag<int>([1, 2, 3], new Random(seed));
            var previous = bag.Next();
            for (var i = 0; i < 30; i++)
            {
                var next = bag.Next();
                Assert.NotEqual(previous, next);
                previous = next;
            }
        }
    }

    [Fact]
    public void ShuffleBag_WithOneItem_JustRepeatsIt()
    {
        var bag = new ShuffleBag<string>(["noot"]);
        Assert.Equal(["noot", "noot", "noot"], [bag.Next(), bag.Next(), bag.Next()]);
    }

    private static string SoundsFolder(params string[] files)
    {
        var folder = Path.Combine(Path.GetTempPath(), "noot-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        foreach (var file in files)
            File.WriteAllBytes(Path.Combine(folder, file), []);
        return folder;
    }

    [Fact]
    public void PlayNext_PlaysOnlyTheNootFiles()
    {
        var folder = SoundsFolder("noot-noot.mp3", "noot_noot.mp3", "pingu-music.mp3", "notes.txt");
        var player = new FakeSoundPlayer();
        var easterEgg = new NootNootEasterEgg(player, folder, new Random(3));

        for (var i = 0; i < 6; i++)
            easterEgg.PlayNext();

        Assert.Equal(6, player.Played.Count);
        Assert.All(player.Played, path => Assert.StartsWith("noot", Path.GetFileName(path)));
        Assert.Equal(2, player.Played.Distinct().Count());
    }

    [Fact]
    public void PlayNext_DoesNothing_WhenThereAreNoSounds()
    {
        var player = new FakeSoundPlayer();

        new NootNootEasterEgg(player, SoundsFolder()).PlayNext();
        new NootNootEasterEgg(player, Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid())).PlayNext();

        Assert.Empty(player.Played);
    }
}
