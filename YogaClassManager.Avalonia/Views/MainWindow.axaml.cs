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

        // Force the side menu to collapse when the window is narrow (MainWindowViewModel.IsNarrow).
        SizeChanged += (_, _) => UpdateNarrow();
        DataContextChanged += (_, _) => UpdateNarrow();
    }

    private void UpdateNarrow()
    {
        if (DataContext is MainWindowViewModel viewModel && ClientSize.Width > 0)
            viewModel.IsNarrow = ClientSize.Width < MainWindowViewModel.NarrowWindowWidth;
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