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
}
