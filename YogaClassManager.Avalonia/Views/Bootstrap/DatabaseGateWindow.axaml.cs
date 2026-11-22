using Avalonia.Controls;

namespace YogaClassManager.Avalonia.Views.Bootstrap;

/// <summary>
///     The startup gate: a real Window (not a DialogHost overlay - see UI_STYLE_GUIDE.md's Dialogs
///     section) shown before MainWindow/DialogHost exist at all, since no repository can be
///     constructed until a database file is resolved.
///
///     Closing it (title-bar close, Cmd+Q, Dock > Quit, Alt+F4) quits the app: it's desktop.MainWindow
///     until a database is bound, under ShutdownMode.OnMainWindowClose. That's what keeps the main app
///     from ever running without a database - so the close is never cancelled; an earlier version that
///     cancelled it left no way to quit at all short of force-quitting. Migrations run in transactions,
///     so quitting while a file is still opening is safe; App.axaml.cs checks <see cref="IsClosed" />
///     before handing off, so a bind that finishes after the user quit doesn't open MainWindow.
/// </summary>
public partial class DatabaseGateWindow : Window
{
    public DatabaseGateWindow()
    {
        InitializeComponent();
        Closed += (_, _) => IsClosed = true;
    }

    /// <summary>True once the window has closed - by the user quitting, or by the hand-off to MainWindow.</summary>
    public bool IsClosed { get; private set; }
}
