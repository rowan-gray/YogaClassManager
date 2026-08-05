using YogaClassManager.Avalonia.ViewModels.Shared;

namespace YogaClassManager.Avalonia.Services;

public static class DialogServiceExtensions
{
    /// <summary>
    ///     Shows the shared yes/no prompt and returns whether the user went ahead. Wraps the
    ///     ConfirmViewModel + ShowDialogAsync + "== true" dance so the destructive commands that need a
    ///     confirmation each stay one readable line - there are enough of them that repeating it
    ///     invites one being written subtly differently (or, as before, skipped entirely).
    ///
    ///     Cancelling returns false, and so does dismissing the dialog, because ShowDialogAsync yields
    ///     default(bool) rather than true for anything but an explicit confirm.
    /// </summary>
    public static async Task<bool> ConfirmAsync(this IDialogService dialogs, string title, string message,
        string confirmLabel = "Confirm", string cancelLabel = "Cancel")
    {
        var viewModel = new ConfirmViewModel(title, message, confirmLabel, cancelLabel);
        return await dialogs.ShowDialogAsync(viewModel) == true;
    }
}
