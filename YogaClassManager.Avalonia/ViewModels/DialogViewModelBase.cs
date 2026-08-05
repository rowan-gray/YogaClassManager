using System.Reactive;
using System.Windows.Input;
using ReactiveUI;

namespace YogaClassManager.Avalonia.ViewModels;

/// <summary>
///     The non-generic surface of a dialog ViewModel, so the shared dialog host (Controls/DialogHost)
///     can drive Escape/Enter without knowing each dialog's TResult. IDialogService.ActiveDialog is
///     typed <see cref="object" /> precisely because TResult varies per dialog, so a cast to
///     DialogViewModelBase&lt;TResult&gt; isn't available there - this interface is.
/// </summary>
public interface IDialogViewModel
{
    /// <summary>What Escape (and a backdrop click) runs. Always present - closing a dialog with no
    /// result is the one thing every dialog can do.</summary>
    ICommand CancelCommand { get; }

    /// <summary>What Enter runs - the dialog's primary/accent action (Save/Select/Confirm/Link/...).
    /// Null for a dialog with no single obvious default. Enter is only honoured when the command's own
    /// CanExecute allows it, so a half-filled form doesn't submit.</summary>
    ICommand? DefaultCommand { get; }
}

/// <summary>
///     Base for ViewModels shown via IDialogService.ShowDialogAsync - a dialog closes itself by
///     calling Close(result), which the dialog service is listening for.
///
///     CancelCommand lives here rather than being re-declared per dialog: it was byte-identical in all
///     12 dialog ViewModels, and the shared dialog host needs one guaranteed way to cancel any dialog
///     for Escape/backdrop-dismiss to work uniformly. Close(default) is what each of those hand-written
///     copies did - default(TResult?) is null for the reference-typed and Nullable&lt;T&gt; results, and
///     false for ConfirmViewModel's bool, which is exactly the "cancelled" value each caller checks for.
/// </summary>
public abstract class DialogViewModelBase<TResult> : ViewModelBase, IDialogViewModel
{
    protected DialogViewModelBase()
    {
        CancelCommand = ReactiveCommand.Create(() => Close(default));
    }

    public event Action<TResult?>? RequestClose;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    ICommand IDialogViewModel.CancelCommand => CancelCommand;

    /// <summary>Set by each subclass to its own primary command (see <see cref="IDialogViewModel" />).
    /// Left null by a dialog whose primary action shouldn't fire on a stray Enter.</summary>
    public ICommand? DefaultCommand { get; protected set; }

    protected void Close(TResult? result)
    {
        RequestClose?.Invoke(result);
    }
}
