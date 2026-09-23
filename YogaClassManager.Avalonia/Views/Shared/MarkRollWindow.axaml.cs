using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using YogaClassManager.Avalonia.ViewModels.Shared;

namespace YogaClassManager.Avalonia.Views.Shared;

public partial class MarkRollWindow : Window
{
    private bool forceClose;
    private IDisposable? saveSubscription;

    public MarkRollWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;

        // DataContext is set (via object initializer) before RollWindowService calls Show(), so it's
        // already available by the time this fires - same timing MainWindow.axaml.cs relies on for its
        // own Opened handler. Closing the window on save completion here (rather than a Click handler
        // on the Save button) keeps "Save" a plain Command binding, so it stays auto-disabled via
        // SaveCommand's CanExecute (IsDirty) like every other command-bound button in this app.
        if (DataContext is MarkRollViewModel viewModel)
            saveSubscription = viewModel.SaveCommand.Subscribe(_ => CloseForced());
    }

    /// <summary>
    ///     Window-wide shortcuts. Handled here rather than as Window.KeyBindings so the
    ///     already-handled check is explicit: when this window's own DialogHost has a dialog open it
    ///     consumes Escape itself (closing that dialog), and Escape must not also close the window out
    ///     from under it.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || DataContext is not MarkRollViewModel viewModel || viewModel.Dialogs.IsDialogOpen)
            return;

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            // Not CloseForced: Close() goes through OnClosing, which is where the discard-changes
            // prompt lives, so Escape can't silently throw away an unsaved roll.
            Close();
        }
        else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            // Via ICommand rather than ReactiveCommand.Execute() so this goes through exactly the path
            // the Save button's binding uses - including the CanExecute (IsDirty) gate and the OnOpened
            // subscription that closes the window once a save completes.
            ICommand save = viewModel.SaveCommand;
            if (!save.CanExecute(null))
                return;

            e.Handled = true;
            save.Execute(null);
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (forceClose || DataContext is not MarkRollViewModel viewModel)
            return;

        e.Cancel = true;

        if (await viewModel.ConfirmDiscardChangesAsync())
            CloseForced();
    }

    private async void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MarkRollViewModel viewModel && !await viewModel.ConfirmDiscardChangesAsync())
            return;

        CloseForced();
    }

    /// <summary>Public so IRollWindowService.CloseAll() can force-close this window from the outside
    /// (e.g. when the app's database is about to be hot-swapped) without going through the normal
    /// discard-changes confirmation OnClosing owns - a database swap discards in-progress roll marking
    /// unconditionally, there's no "keep editing against the old database" option.</summary>
    public void CloseForced()
    {
        forceClose = true;
        saveSubscription?.Dispose();
        Close();
    }
}
