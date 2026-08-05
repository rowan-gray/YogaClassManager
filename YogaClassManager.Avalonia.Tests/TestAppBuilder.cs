using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using YogaClassManager.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Boots the real App (so views resolve the same Styles/Tokens the running app uses) on Avalonia's
///     headless platform, enabling [AvaloniaFact] view tests. Without this, XAML load failures and
///     unresolved binding paths in a view are only discoverable by launching the desktop app and
///     navigating to the affected page by hand - the build's XAML compiler validates markup, not
///     binding paths against a real DataContext.
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<global::YogaClassManager.Avalonia.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            // Required for the same reason Program.cs needs it: ReactiveUserControl's constructor calls
            // WhenActivated, which throws without ReactiveUI's Avalonia IActivationForViewFetcher.
            .UseReactiveUI();
}
