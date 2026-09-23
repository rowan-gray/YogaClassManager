using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     Opens Mark Roll as a real, non-modal desktop window rather than the shared in-app dialog
///     overlay, so the instructor can keep using the rest of the app while marking a roll.
/// </summary>
public interface IRollWindowService
{
    /// <summary>Opens a Mark Roll window for the given roll, or brings its window to the front if one
    /// is already open for that roll id - never opens a second window for the same roll.
    /// <paramref name="onClosed" /> fires once the window closes, whether or not anything changed, so
    /// callers can refresh whatever list is showing that roll's summary.</summary>
    void OpenOrActivate(ClassRoll roll, Action onClosed);

    /// <summary>Force-closes every currently open Mark Roll window. Needed when the app's database is
    /// about to be hot-swapped (see IAppDataStoreProvider.SwapToAsync) - an open Mark Roll window holds
    /// direct repository references from the database being swapped away from, with no reconciliation
    /// story for "the roll I'm halfway through marking now belongs to a different database".</summary>
    void CloseAll();
}
