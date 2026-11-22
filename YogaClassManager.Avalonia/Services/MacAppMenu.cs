using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     Tidies the macOS application menu (the bold "Yoga Class Manager" menu): removes Avalonia's
///     "About Avalonia" item (the framework's credits, not this app's), keeping the standard Services /
///     Hide / Hide Others / Show All / Quit.
///
///     This edits the native NSMenu because there's no Avalonia API for it: Avalonia adds its default
///     items on the native side only (not to any NativeMenu the app can see), and its one switch,
///     MacOSPlatformOptions.DisableDefaultApplicationMenuItems, drops Services/Hide/Hide Others/Show All/
///     Quit too (and their replacements can't reuse Avalonia's internal commands); setting an empty
///     NativeMenu on the Application doesn't remove it either. Avalonia can rebuild the menu - e.g. when
///     MainWindow replaces the startup gate - so the tidy-up re-runs whenever a window opens; it's
///     idempotent (it removes the item by title, if present).
/// </summary>
internal static class MacAppMenu
{
    private static readonly string[] RemovedTitles = ["About Avalonia"];

    public static void Attach()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        // Posted, not run inline: Avalonia (re)builds the native menu in response to the same events.
        void Schedule(object? sender, RoutedEventArgs e) =>
            Dispatcher.UIThread.Post(RemoveUnwantedItems, DispatcherPriority.Background);

        Window.WindowOpenedEvent.AddClassHandler<Window>(Schedule);
    }

    private static void RemoveUnwantedItems()
    {
        var nsApp = Send(objc_getClass("NSApplication"), "sharedApplication");
        var mainMenu = Send(nsApp, "mainMenu");
        if (mainMenu == IntPtr.Zero || (long)Send(mainMenu, "numberOfItems") == 0)
            return;

        var appMenu = Send(SendLong(mainMenu, "itemAtIndex:", 0), "submenu");
        if (appMenu == IntPtr.Zero)
            return;

        for (var i = (long)Send(appMenu, "numberOfItems") - 1; i >= 0; i--)
        {
            var item = SendLong(appMenu, "itemAtIndex:", i);
            if (!RemovedTitles.Contains(Title(item)))
                continue;

            // Take the separator that followed it too, so no doubled-up separator lines are left.
            if (i + 1 < (long)Send(appMenu, "numberOfItems") &&
                Send(SendLong(appMenu, "itemAtIndex:", i + 1), "isSeparatorItem") != IntPtr.Zero)
                SendLong(appMenu, "removeItemAtIndex:", i + 1);
            SendLong(appMenu, "removeItemAtIndex:", i);
        }
    }

    private static string Title(IntPtr item)
    {
        var title = Send(item, "title");
        return title == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(Send(title, "UTF8String")) ?? "";
    }

    private static IntPtr Send(IntPtr receiver, string selector) =>
        objc_msgSend(receiver, sel_registerName(selector));

    private static IntPtr SendLong(IntPtr receiver, string selector, long argument) =>
        objc_msgSend_long(receiver, sel_registerName(selector), argument);

    private const string ObjCRuntime = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjCRuntime)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCRuntime)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCRuntime)]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_long(IntPtr receiver, IntPtr selector, long argument);
}
