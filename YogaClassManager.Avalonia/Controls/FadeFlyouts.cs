using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Fades a flyout's presenter in when it opens and out before it closes (120ms - inside the app's
///     ~200ms animation ceiling, UI_STYLE_GUIDE.md "Animation"). The presenter's Opacity rests at 0 while
///     closed and animates to 1 on open; a close request - a menu item, Escape, a click outside - is
///     cancelled, the presenter fades to 0, then the flyout really closes.
/// </summary>
internal sealed class FlyoutFader
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(120);

    private readonly PopupFlyoutBase flyout;
    private Control? presenter;
    private bool closeAllowed;
    private bool fadingOut;

    public FlyoutFader(PopupFlyoutBase flyout) => this.flyout = flyout;

    /// <summary>Call from CreatePresenter: starts the presenter invisible, with an Opacity transition.</summary>
    public Control Prepare(Control created)
    {
        presenter = created;
        created.Opacity = 0;
        created.Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = Duration }];
        return created;
    }

    /// <summary>Call from OnOpened: fade in.</summary>
    public void Opened()
    {
        closeAllowed = false;
        fadingOut = false;
        if (presenter is not null)
            presenter.Opacity = 1;
    }

    /// <summary>Call from OnClosing: hold the close until the fade-out has played.</summary>
    public void Closing(CancelEventArgs args)
    {
        if (closeAllowed || presenter is null)
            return;

        args.Cancel = true;
        if (fadingOut)
            return; // already on its way out

        fadingOut = true;
        presenter.Opacity = 0;
        _ = Task.Delay(Duration).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            closeAllowed = true;
            flyout.Hide();
        }), TaskContinuationOptions.OnlyOnRanToCompletion);
    }
}

/// <summary>A <see cref="MenuFlyout" /> (the "..." action menus) that fades in and out - see
/// <see cref="FlyoutFader" />. Use it wherever you'd use MenuFlyout.</summary>
public class FadeMenuFlyout : MenuFlyout
{
    private readonly FlyoutFader fader;

    public FadeMenuFlyout() => fader = new FlyoutFader(this);

    protected override Control CreatePresenter() => fader.Prepare(base.CreatePresenter());

    protected override void OnOpened()
    {
        base.OnOpened();
        fader.Opened();
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        base.OnClosing(args);
        if (!args.Cancel)
            fader.Closing(args);
    }
}

/// <summary>A <see cref="Flyout" /> (the sort/filter popups) that fades in and out - see
/// <see cref="FlyoutFader" />. Use it wherever you'd use Flyout.</summary>
public class FadeFlyout : Flyout
{
    private readonly FlyoutFader fader;

    public FadeFlyout() => fader = new FlyoutFader(this);

    protected override Control CreatePresenter() => fader.Prepare(base.CreatePresenter());

    protected override void OnOpened()
    {
        base.OnOpened();
        fader.Opened();
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        base.OnClosing(args);
        if (!args.Cancel)
            fader.Closing(args);
    }
}
