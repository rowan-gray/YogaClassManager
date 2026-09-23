using Avalonia.Controls;

namespace YogaClassManager.Avalonia.Views.Bootstrap;

/// <summary>
///     The startup gate: a real Window (not a DialogHost overlay - see UI_STYLE_GUIDE.md's Dialogs
///     section) shown before MainWindow/DialogHost exist at all, since no repository can be
///     constructed until a database file is resolved. Modeled directly on MarkRollWindow's
///     OnClosing-cancel pattern, but with the opposite modality stance: MarkRollWindow is deliberately
///     non-modal and always closable; this window is fully modal and cannot be dismissed - no Escape,
///     no title-bar close button, no Alt+F4 - until App.axaml.cs calls CloseForced() once a database
///     has actually been bound.
/// </summary>
public partial class DatabaseGateWindow : Window
{
    private bool forceClose;

    public DatabaseGateWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!forceClose)
            e.Cancel = true;
    }

    public void CloseForced()
    {
        forceClose = true;
        Close();
    }
}
