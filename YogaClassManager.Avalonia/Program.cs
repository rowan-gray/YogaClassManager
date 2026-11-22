using Avalonia;
using Avalonia.ReactiveUI;
using System;

namespace YogaClassManager.Avalonia;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Error logging is turned on here, by the real app's entry point only - see
            // App.ConfigureErrorLogging. AfterSetup runs once the platform is up, before App's startup.
            BuildAvaloniaApp()
                .AfterSetup(_ => App.ConfigureErrorLogging())
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            // A crash that escaped everything else: write it down before the process goes.
            Services.ErrorLog.Error("Fatal error - the app is closing", exception);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
