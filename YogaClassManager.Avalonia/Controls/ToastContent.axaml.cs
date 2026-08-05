using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     A toast's inner content: a white icon on a solid severity-colored badge, plus a muted
///     severity-colored message. Shown via WindowNotificationManager.Show(object, ...) as the
///     "content" of a plain NotificationCard (see Services/ToastService.cs) - not reusing
///     Notification/INotification's own title+message layout, since that has no icon slot.
///     Severity never changes after construction, so this is set once here rather than via a
///     bindable StyledProperty - the "Error"/"Warning"/"Information" class drives the icon
///     kind/colors via Styles/Toasts.axaml.
/// </summary>
public partial class ToastContent : UserControl
{
    public ToastContent()
    {
        InitializeComponent();
    }

    public ToastContent(NotificationType type, string message) : this()
    {
        Root.Classes.Add(type.ToString());
        MessageText.Text = message;

        // Severity is otherwise carried by the badge icon and colour alone - neither of which a screen
        // reader announces, and colour alone is not a sufficient distinction regardless. Naming the
        // severity in the accessible name is the text equivalent of the badge.
        AutomationProperties.SetName(Root, $"{SeverityLabel(type)}: {message}");
    }

    private static string SeverityLabel(NotificationType type) => type switch
    {
        NotificationType.Error => "Error",
        NotificationType.Warning => "Warning",
        NotificationType.Success => "Success",
        _ => "Information"
    };
}
