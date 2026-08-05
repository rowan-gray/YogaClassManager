using Avalonia.Controls;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     Transient, top-level user feedback ("Added John Smith.", "Can't delete X - it still has
///     linked classes.") - replaces the old per-page StatusMessage text line. ViewModels call
///     ShowInfo/Warning/Error without touching Avalonia's notification types directly; only
///     MainWindow.axaml.cs's Opened handler calls AttachHost, once the app's TopLevel exists.
/// </summary>
public interface IToastService
{
    void AttachHost(TopLevel topLevel);
    void ShowInfo(string message);
    void ShowWarning(string message);
    void ShowError(string message);
}
