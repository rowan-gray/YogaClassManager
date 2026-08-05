using Avalonia.Controls;
using Splat;
using YogaClassManager.Avalonia.Services;

namespace YogaClassManager.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;

        // Attached here rather than the constructor - the AdornerLayer WindowNotificationManager
        // installs into isn't guaranteed to exist until the window's template is applied/it's
        // attached to the visual tree.
        Locator.Current.GetService<IToastService>()!.AttachHost(this);
    }
}