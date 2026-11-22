using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels;
using YogaClassManager.Avalonia.ViewModels.Shared;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Covers UI_REVIEW.md finding U7 - the keyboard contract every dialog now inherits from
///     Controls/DialogHost. These assert against real key input on the headless platform rather than
///     calling the commands directly, because the thing at risk of regressing is the routing (does the
///     key actually reach the host, is focus inside the card at all), not the commands themselves.
/// </summary>
public class DialogKeyboardTests
{
    private static (Window Window, DialogHost Host, DialogService Dialogs) ShowHost()
    {
        var dialogs = new DialogService();
        var host = new DialogHost { DialogService = dialogs };
        var window = new Window { Content = host, Width = 800, Height = 600 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, host, dialogs);
    }

    private static Task<TResult?> OpenDialog<TResult>(DialogService dialogs, DialogViewModelBase<TResult> viewModel)
    {
        var result = dialogs.ShowDialogAsync(viewModel);
        Dispatcher.UIThread.RunJobs();
        return result;
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void OpeningADialog_MovesFocusIntoIt()
    {
        var (window, host, dialogs) = ShowHost();

        _ = OpenDialog(dialogs, new TextInputViewModel("Add health concern", "Concern"));

        var card = host.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("Card"));
        var focused = window.FocusManager?.GetFocusedElement() as Visual;

        Assert.NotNull(focused);
        Assert.True(card.IsVisualAncestorOf(focused),
            "focus should start inside the dialog card - without it, Escape/Enter never reach the host " +
            "and Tab walks into the page behind.");
    }

    [AvaloniaFact]
    public void Escape_CancelsTheDialog()
    {
        var (window, _, dialogs) = ShowHost();

        var result = OpenDialog(dialogs, new ConfirmViewModel("Delete term?", "This cannot be undone.",
            "Delete"));

        Press(window, PhysicalKey.Escape);

        Assert.True(result.IsCompleted);
        Assert.False(result.Result);
        Assert.False(dialogs.IsDialogOpen);
    }

    [AvaloniaFact]
    public void Enter_RunsTheDefaultCommand_FromAField()
    {
        var (window, _, dialogs) = ShowHost();

        var result = OpenDialog(dialogs, new TextInputViewModel("Add health concern", "Concern", "Asthma"));

        Press(window, PhysicalKey.Enter);

        Assert.True(result.IsCompleted);
        Assert.Equal("Asthma", result.Result);
    }

    [AvaloniaFact]
    public void APromptWithNoFields_StartsOnTheSafeButton_SoEnterCannotConfirmIt()
    {
        var (window, host, dialogs) = ShowHost();

        var result = OpenDialog(dialogs, new ConfirmViewModel("Delete term?", "This cannot be undone.",
            "Delete"));

        var buttons = host.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Same(buttons[0], window.FocusManager?.GetFocusedElement());
        Assert.Equal("Cancel", buttons[0].Content);

        // A focused Button owns Enter, so this cancels rather than deleting. That is the point: one
        // keystroke should never be able to confirm a destructive prompt.
        Press(window, PhysicalKey.Enter);

        Assert.True(result.IsCompleted);
        Assert.False(result.Result);
    }

    [AvaloniaFact]
    public void Enter_DoesNothing_WhenTheDefaultCommandCannotExecute()
    {
        var (window, _, dialogs) = ShowHost();

        // TextInputViewModel's SaveCommand is gated on a non-blank value, so an untouched prompt must
        // not submit - the guard that stops Enter bypassing every dialog's own validation gate.
        var result = OpenDialog(dialogs, new TextInputViewModel("Add health concern", "Concern"));

        Press(window, PhysicalKey.Enter);

        Assert.False(result.IsCompleted);
        Assert.True(dialogs.IsDialogOpen);
    }

    [AvaloniaFact]
    public void ClosingADialog_RestoresFocusToWhereItWas()
    {
        var dialogs = new DialogService();
        var outsideButton = new Button { Content = "Add student" };
        var host = new DialogHost { DialogService = dialogs };
        var window = new Window
        {
            Content = new Panel { Children = { outsideButton, host } },
            Width = 800,
            Height = 600
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        outsideButton.Focus();
        Dispatcher.UIThread.RunJobs();

        _ = OpenDialog(dialogs, new ConfirmViewModel("Delete term?", "This cannot be undone."));
        Press(window, PhysicalKey.Escape);

        Assert.Same(outsideButton, window.FocusManager?.GetFocusedElement());
    }

    [AvaloniaFact]
    public void AnOpenDialog_TakesTheContentBehindItOutOfTabOrder()
    {
        var dialogs = new DialogService();
        var pageContent = new Panel { Children = { new Button { Content = "Add student" } } };
        var host = new DialogHost { DialogService = dialogs };
        var window = new Window
        {
            Content = new Panel { Children = { pageContent, host } },
            Width = 800,
            Height = 600
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(KeyboardNavigationMode.Continue, KeyboardNavigation.GetTabNavigation(pageContent));

        _ = OpenDialog(dialogs, new ConfirmViewModel("Delete term?", "This cannot be undone."));
        Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(pageContent));

        Press(window, PhysicalKey.Escape);
        Assert.Equal(KeyboardNavigationMode.Continue, KeyboardNavigation.GetTabNavigation(pageContent));
    }

    [AvaloniaFact]
    public void FormField_AssociatesItsLabelWithTheInput()
    {
        var (_, host, dialogs) = ShowHost();

        _ = OpenDialog(dialogs, new TextInputViewModel("Add health concern", "Concern"));

        var field = Assert.Single(host.GetVisualDescendants().OfType<FormField>());
        var input = Assert.IsType<TextBox>(field.Content);

        var label = AutomationProperties.GetLabeledBy(input);
        Assert.NotNull(label);
        Assert.Equal("Concern", Assert.IsType<TextBlock>(label).Text);
    }

    [AvaloniaFact]
    public void DialogButtonRow_WiresBothButtonsToTheDialogsOwnCommands()
    {
        var (_, host, dialogs) = ShowHost();

        var viewModel = new ConfirmViewModel("Delete term?", "This cannot be undone.", "Delete", "Keep");
        _ = OpenDialog(dialogs, viewModel);

        var row = Assert.Single(host.GetVisualDescendants().OfType<DialogButtonRow>());
        var buttons = row.GetVisualDescendants().OfType<Button>().ToList();

        Assert.Equal(2, buttons.Count);
        Assert.Equal("Keep", buttons[0].Content);
        Assert.Same(viewModel.CancelCommand, buttons[0].Command);

        // The accent button and Enter must be the same command - that equivalence is the whole reason
        // the row binds DefaultCommand rather than taking a per-dialog command property.
        Assert.Equal("Delete", buttons[1].Content);
        Assert.Same(viewModel.DefaultCommand, buttons[1].Command);
        Assert.Same(viewModel.ConfirmCommand, buttons[1].Command);
    }

    /// <summary>A dialog closes only through one of its actions: a stray click on the dimmed backdrop
    /// used to cancel it, throwing away a half-filled form.</summary>
    [AvaloniaFact]
    public void ClickingTheBackdrop_DoesNotCloseTheDialog()
    {
        var (window, _, dialogs) = ShowHost();

        var result = OpenDialog(dialogs, new TextInputViewModel("Add health concern", "Concern", "Asthma"));

        // The window's corner: on the backdrop, well outside the centred card.
        window.MouseDown(new Point(5, 5), MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(new Point(5, 5), MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(result.IsCompleted);
        Assert.True(dialogs.IsDialogOpen);
    }

    private static void PumpFor(TimeSpan duration)
    {
        var until = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The card used to empty the instant a dialog closed, leaving a small square (just its
    /// padding and border) in the middle of the screen for the overlay's 150ms fade-out. It now keeps
    /// showing the closing dialog until the fade has finished.</summary>
    [AvaloniaFact]
    public void AClosedDialog_StaysInTheCardThroughTheFadeOut_ThenClears()
    {
        var (_, host, dialogs) = ShowHost();
        var confirm = new ConfirmViewModel("Delete this roll?", "This can't be undone.", "Delete");
        _ = OpenDialog(dialogs, confirm);
        Assert.Same(confirm, host.DisplayedDialog);

        ((System.Windows.Input.ICommand)confirm.CancelCommand).Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(dialogs.IsDialogOpen);
        Assert.Same(confirm, host.DisplayedDialog); // still drawn while the overlay fades

        PumpFor(TimeSpan.FromMilliseconds(400));
        Assert.Null(host.DisplayedDialog);
    }

    [AvaloniaFact]
    public void ANewDialog_OpenedDuringTheFadeOut_ReplacesTheOldOneImmediately()
    {
        var (_, host, dialogs) = ShowHost();
        var first = new ConfirmViewModel("First?", "", "OK");
        _ = OpenDialog(dialogs, first);
        ((System.Windows.Input.ICommand)first.CancelCommand).Execute(null);
        Dispatcher.UIThread.RunJobs();

        var second = new ConfirmViewModel("Second?", "", "OK");
        _ = OpenDialog(dialogs, second);
        Assert.Same(second, host.DisplayedDialog);

        PumpFor(TimeSpan.FromMilliseconds(400)); // the first dialog's pending clear must not blank it
        Assert.Same(second, host.DisplayedDialog);
    }
}
