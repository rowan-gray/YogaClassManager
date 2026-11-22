using System.Runtime.CompilerServices;
using YogaClassManager.Avalonia.ViewModels;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>Runs once, before any test: turns the load debounce off so page/selection loads stay
/// immediate (most tests assert straight after constructing a view model or changing its selection).
/// LoadDebounceTests turns it back on, in a collection that never runs alongside other tests.</summary>
internal static class TestDefaults
{
    [ModuleInitializer]
    internal static void Initialize() => ViewModelBase.LoadDebounce = TimeSpan.Zero;
}
