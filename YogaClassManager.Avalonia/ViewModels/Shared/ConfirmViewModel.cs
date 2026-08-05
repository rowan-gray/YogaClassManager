using System.Reactive;
using ReactiveUI;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>A generic yes/no confirmation prompt - e.g. "discard unsaved changes?" before closing a
/// window with a save/cancel paradigm. Not tied to any one flow, unlike MergeConfirmViewModel/
/// PromoteConfirmViewModel, which each show a specific computed summary for their own operation.</summary>
public class ConfirmViewModel : DialogViewModelBase<bool>
{
    public ConfirmViewModel(string title, string message, string confirmLabel = "Confirm",
        string cancelLabel = "Cancel")
    {
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        CancelLabel = cancelLabel;

        ConfirmCommand = ReactiveCommand.Create(() => Close(true));
        DefaultCommand = ConfirmCommand;
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmLabel { get; }
    public string CancelLabel { get; }

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
}
