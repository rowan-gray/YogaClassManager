using System.Diagnostics;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     ReactiveUI's documented extension point for exceptions no one subscribed to on a
///     ReactiveCommand's ThrownExceptions - wired as RxApp.DefaultExceptionHandler in
///     App.axaml.cs so a failed command surfaces as an error toast app-wide instead of silently
///     stopping the command (release) or crashing the debugger (debug), without needing per-command
///     try/catch. Commands with their own try/catch producing a specific message never reach this.
/// </summary>
public class ToastExceptionHandler(IToastService toastService) : IObserver<Exception>
{
    public void OnNext(Exception error)
    {
        Debug.WriteLine($"Unhandled command exception: {error}");
        toastService.ShowError("Something went wrong. Please try again.");
    }

    public void OnError(Exception error)
    {
    }

    public void OnCompleted()
    {
    }
}
