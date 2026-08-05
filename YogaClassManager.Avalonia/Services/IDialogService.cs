using System.ComponentModel;
using YogaClassManager.Avalonia.ViewModels;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     Shows a ViewModel as a modal dialog, rendered as an in-app overlay (see MainWindow.axaml's
///     dialog overlay layer) rather than a separate OS window. Used for the flows that were MAUI
///     modal "with callback" pages (Add/Edit Pass, Search/Pick Identity, merge picker) rather than
///     router navigation - those don't belong in the back-stack.
/// </summary>
public interface IDialogService : INotifyPropertyChanged
{
    /// <summary>The topmost open dialog's ViewModel, or null if none is open. Supports nesting - a
    /// dialog can open another dialog on top of itself (e.g. Add Pass -> Add Alteration).</summary>
    object? ActiveDialog { get; }

    bool IsDialogOpen { get; }

    Task<TResult?> ShowDialogAsync<TResult>(DialogViewModelBase<TResult> viewModel);
}
