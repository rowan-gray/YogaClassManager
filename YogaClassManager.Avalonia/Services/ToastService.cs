using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using YogaClassManager.Avalonia.Controls;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     Wraps Avalonia's built-in WindowNotificationManager (renders inside the host TopLevel's
///     AdornerLayer - an in-window overlay, not a new OS window) rather than hand-rolling a toast
///     stack. AttachHost must be called once the app's TopLevel exists (see MainWindow.axaml.cs's
///     Opened handler) before any Show* call actually displays anything. MaxItems=5 caps how many
///     stack at once - WindowNotificationManager pops the oldest itself once a new one would exceed
///     that, so no manual queue management is needed here.
/// </summary>
public class ToastService : IToastService
{
    private WindowNotificationManager? manager;

    public void AttachHost(TopLevel topLevel)
    {
        manager = new WindowNotificationManager(topLevel)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 5
        };
    }

    public void ShowInfo(string message) => Show(message, NotificationType.Information);

    public void ShowWarning(string message) => Show(message, NotificationType.Warning);

    public void ShowError(string message) => Show(message, NotificationType.Error);

    private void Show(string message, NotificationType type)
    {
        manager?.Show(new ToastContent(type, message), type);
    }
}
