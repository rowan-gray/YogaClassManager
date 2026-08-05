using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using YogaClassManager.Avalonia.Views;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Loads the XAML of every View in the app. Cheap, but it catches the one failure mode the compiler
///     cannot: a <c>{StaticResource X}</c> whose key doesn't exist throws when the XAML is *loaded*, not
///     when it's built. With the design tokens now bound rather than written as literals, that is the
///     realistic way to break a view - and until this existed, most views were only proved to load by
///     launching the app and navigating to them by hand.
///
///     ViewSmokeTests goes further for a few views (real ViewModel, real bindings, assertions about
///     what rendered); this is the floor under all of them.
/// </summary>
public class ViewLoadTests
{
    public static TheoryData<Type> AllViewTypes()
    {
        var data = new TheoryData<Type>();

        foreach (var type in typeof(MainWindow).Assembly
                     .GetTypes()
                     .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false }
                                 && typeof(Control).IsAssignableFrom(t)
                                 && t.Namespace?.StartsWith("YogaClassManager.Avalonia.Views") == true
                                 && t.GetConstructor(Type.EmptyTypes) is not null)
                     .OrderBy(t => t.FullName))
            data.Add(type);

        return data;
    }

    [AvaloniaTheory]
    [MemberData(nameof(AllViewTypes))]
    public void EveryView_LoadsItsXaml(Type viewType)
    {
        var view = Activator.CreateInstance(viewType);

        Assert.NotNull(view);
    }

    [AvaloniaFact]
    public void TheViewSetIsNotEmpty()
    {
        // Guards the reflection above: a namespace rename that matched nothing would otherwise turn the
        // theory into zero silently-passing cases.
        Assert.True(AllViewTypes().Count() >= 20,
            "expected the Views namespace to contain every page and dialog");
    }
}
