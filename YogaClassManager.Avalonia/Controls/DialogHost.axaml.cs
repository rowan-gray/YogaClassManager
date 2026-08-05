using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     The shared dialog overlay - a dimmed backdrop and a centered card rendering an IDialogService's
///     ActiveDialog - plus the keyboard behaviour that makes a dialog usable without a mouse:
///
///     <list type="bullet">
///         <item>Escape (and a backdrop click) runs the dialog's CancelCommand.</item>
///         <item>Enter runs its DefaultCommand, when that command's own CanExecute allows it.</item>
///         <item>Opening a dialog moves focus to its first focusable control; closing puts focus back
///               where it was, so the keyboard doesn't lose its place mid-task.</item>
///         <item>Tab cycles within the card (see the XAML) and the content behind is taken out of tab
///               order entirely while a dialog is open, so focus can't walk behind the modal.</item>
///     </list>
///
///     This exists as one control rather than a copy of the overlay markup per window precisely because
///     that behaviour would otherwise have to be written twice (MainWindow and MarkRollWindow both host
///     dialogs, against different IDialogService instances) and would drift.
/// </summary>
public partial class DialogHost : UserControl
{
    public static readonly StyledProperty<IDialogService?> DialogServiceProperty =
        AvaloniaProperty.Register<DialogHost, IDialogService?>(nameof(DialogService));

    public static readonly StyledProperty<double> MaxDialogWidthProperty =
        AvaloniaProperty.Register<DialogHost, double>(nameof(MaxDialogWidth), 600);

    public static readonly StyledProperty<double> MaxDialogHeightProperty =
        AvaloniaProperty.Register<DialogHost, double>(nameof(MaxDialogHeight), 700);

    private IDialogService? subscribed;
    private IInputElement? focusBeforeDialog;
    private bool isAttached;
    private bool isTrappingFocus;

    /// <summary>Guards the queued initial-focus callbacks below: a dialog closed (or replaced by a
    /// nested one) between queueing and running must not have focus yanked into a card that is now
    /// showing something else.</summary>
    private int focusGeneration;

    static DialogHost()
    {
        DialogServiceProperty.Changed.AddClassHandler<DialogHost>((host, _) => host.Resubscribe());
    }

    public DialogHost()
    {
        InitializeComponent();

        // Bubble, not tunnel: everything with a legitimate claim on Enter/Escape (a multi-line TextBox,
        // NumericUpDown committing its typed text, an open ComboBox dropdown or Flyout, a focused
        // Button) sits below this in the tree and marks the event handled first. Tunnelling would
        // preempt all of them.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    /// <summary>Which dialog stack this host renders. Both the app-wide singleton (MainWindow) and
    /// MarkRollViewModel's own private instance are valid values - see UI_STYLE_GUIDE.md's Dialogs
    /// section.</summary>
    public IDialogService? DialogService
    {
        get => GetValue(DialogServiceProperty);
        set => SetValue(DialogServiceProperty, value);
    }

    public double MaxDialogWidth
    {
        get => GetValue(MaxDialogWidthProperty);
        set => SetValue(MaxDialogWidthProperty, value);
    }

    public double MaxDialogHeight
    {
        get => GetValue(MaxDialogHeightProperty);
        set => SetValue(MaxDialogHeightProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        isAttached = true;
        Resubscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        isAttached = false;
        Resubscribe();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Keeps the PropertyChanged hook matched to (attached AND current DialogService). An
    /// IDialogService outlives the host that renders it (the app-wide one is a singleton), so leaving
    /// this subscribed past detach would pin the host - the exact leak shape UI_REVIEW.md's A10 warns
    /// about for the first subscription anyone writes against a service.</summary>
    private void Resubscribe()
    {
        var target = isAttached ? DialogService : null;
        if (ReferenceEquals(subscribed, target))
            return;

        if (subscribed is not null)
            subscribed.PropertyChanged -= OnDialogServiceChanged;

        subscribed = target;

        if (subscribed is not null)
            subscribed.PropertyChanged += OnDialogServiceChanged;

        UpdateForDialogState();
    }

    private void OnDialogServiceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IDialogService.ActiveDialog) or nameof(IDialogService.IsDialogOpen))
            UpdateForDialogState();
    }

    private void UpdateForDialogState()
    {
        var isOpen = subscribed?.IsDialogOpen == true;

        if (isOpen && !isTrappingFocus)
        {
            focusBeforeDialog = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            SetContentBehindTabNavigation(KeyboardNavigationMode.None);
            isTrappingFocus = true;
        }
        else if (!isOpen && isTrappingFocus)
        {
            SetContentBehindTabNavigation(KeyboardNavigationMode.Continue);
            isTrappingFocus = false;
            focusGeneration++;

            focusBeforeDialog?.Focus();
            focusBeforeDialog = null;
            return;
        }

        // Also runs when ActiveDialog changes while a dialog is already open (a nested dialog opening,
        // or a nested one closing to reveal its parent) - focus belongs in whatever is on top now.
        if (isOpen)
            QueueInitialFocus();
    }

    private void QueueInitialFocus()
    {
        var generation = ++focusGeneration;

        // The dialog's View is built by the ContentControl during the next layout pass, so there is
        // nothing focusable in the card yet at the moment ActiveDialog changes. Loaded priority runs
        // after that pass; the one Background-priority retry covers a dialog whose own content
        // materialises a frame later still (e.g. an ItemsControl filled from a completed Task).
        Dispatcher.UIThread.Post(() => TrySetInitialFocus(generation, retry: true), DispatcherPriority.Loaded);
    }

    private void TrySetInitialFocus(int generation, bool retry)
    {
        if (generation != focusGeneration || subscribed?.IsDialogOpen != true)
            return;

        var focusable = PART_Card.GetVisualDescendants()
            .OfType<InputElement>()
            .Where(control => control is { Focusable: true, IsEffectivelyEnabled: true }
                              && control.IsEffectivelyVisible)
            .ToList();

        // Prefer the first real input over the first Button. Two reasons, both about Enter: a focused
        // Button consumes Enter itself (activating that button rather than the dialog's DefaultCommand),
        // and landing in the first field is what removes the "click before you can type" complaint in
        // the first place. A prompt with no fields at all therefore starts on Cancel - the first button
        // in the row and the safe one - so Enter on a "Delete this permanently?" prompt cannot confirm
        // it with a single keystroke. Confirming is deliberately Tab-then-Enter, or a click.
        var target = focusable.FirstOrDefault(control => control is not Button) ?? focusable.FirstOrDefault();

        if (target is not null)
        {
            // Tab (rather than Pointer) so the focus-visible adorner is drawn - a dialog that silently
            // focused its first field would leave a keyboard user with no idea where they are.
            target.Focus(NavigationMethod.Tab);
            return;
        }

        if (retry)
            Dispatcher.UIThread.Post(() => TrySetInitialFocus(generation, retry: false),
                DispatcherPriority.Background);
    }

    /// <summary>Blanks tab navigation on whatever this host is layered over (its siblings in the
    /// hosting Panel - the SplitView in MainWindow, the roll form in MarkRollWindow) so Tab can't reach
    /// controls the dimmed backdrop has already made unclickable.</summary>
    private void SetContentBehindTabNavigation(KeyboardNavigationMode mode)
    {
        if (Parent is not Panel panel)
            return;

        foreach (var sibling in panel.Children)
            if (!ReferenceEquals(sibling, this))
                KeyboardNavigation.SetTabNavigation(sibling, mode);
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only a press on the backdrop itself dismisses - a press anywhere inside the card bubbles up
        // here too, and dismissing on that would make the dialog impossible to use.
        if (e.Source is Visual source && PART_Card.IsVisualAncestorOf(source))
            return;

        if (subscribed?.ActiveDialog is IDialogViewModel dialog)
            TryInvoke(dialog.CancelCommand, () => e.Handled = true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || subscribed?.ActiveDialog is not IDialogViewModel dialog)
            return;

        switch (e.Key)
        {
            case Key.Escape:
                TryInvoke(dialog.CancelCommand, () => e.Handled = true);
                break;

            case Key.Enter:
                // A multi-line box owns Enter outright; submitting from one would make it impossible to
                // type a newline.
                if (e.Source is TextBox { AcceptsReturn: true })
                    return;

                if (dialog.DefaultCommand is { } command)
                    TryInvoke(command, () => e.Handled = true);
                break;
        }
    }

    private static void TryInvoke(ICommand command, Action markHandled)
    {
        if (!command.CanExecute(null))
            return;

        command.Execute(null);
        markHandled();
    }
}
