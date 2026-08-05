using YogaClassManager.Avalonia.Services;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Records which rolls were opened instead of showing a Window, so roll-opening flows can be tested
///     headlessly. Exposes the roll instances themselves, which is what lets a test assert whether a
///     roll had already been persisted (Id != 0) at the moment the window was asked for.
/// </summary>
public class FakeRollWindowService : IRollWindowService
{
    public List<ClassRoll> Opened { get; } = [];
    public List<Action> OnClosedCallbacks { get; } = [];

    public void OpenOrActivate(ClassRoll roll, Action onClosed)
    {
        Opened.Add(roll);
        OnClosedCallbacks.Add(onClosed);
    }
}
