using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Behaviors;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Avalonia.Views.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     ScrollEdgeShadow draws into an AdornerLayer. The window's own top-level layer paints above all
///     regular content - including the DialogHost overlay - so the shadows showed through open dialogs.
///     Each window therefore wraps its page content (not the DialogHost) in a VisualLayerManager, giving
///     the page's shadows a layer that is painted underneath the dialog overlay.
/// </summary>
public class ScrollEdgeShadowLayeringTests
{
    [AvaloniaFact]
    public void MarkRollWindow_PageShadows_DrawBelowTheDialogOverlay()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes,
            new InMemoryEmergencyContactRepository(store));
        var viewModel = new MarkRollViewModel(
            new ClassRoll(0, new DateOnly(2026, 7, 20), new ClassSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), false), []),
            new InMemoryClassRollRepository(store), students, passes, new InMemoryTermRepository(store),
            new FakeToastService());

        var window = new MarkRollWindow { DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var dialogHost = Assert.Single(window.GetVisualDescendants().OfType<DialogHost>());
        var shadowed = window.GetVisualDescendants().OfType<ScrollViewer>()
            .Where(sv => ScrollEdgeShadow.GetIsEnabled(sv) ||
                         (sv.TemplatedParent is Control owner && ScrollEdgeShadow.GetIsEnabled(owner)))
            .ToList();
        Assert.NotEmpty(shadowed);

        foreach (var scrollViewer in shadowed)
        {
            var layer = AdornerLayer.GetAdornerLayer(scrollViewer);
            Assert.NotNull(layer);
            Assert.NotEmpty(layer.Children); // the shadow bars were mounted here

            // The layer's manager must not also contain the dialog overlay - if it did, it would be the
            // window's top-level layer, painted above the dialog.
            var manager = layer.FindAncestorOfType<VisualLayerManager>();
            Assert.NotNull(manager);
            Assert.False(manager.IsVisualAncestorOf(dialogHost),
                "a page scroll shadow is drawing in the window's top-level adorner layer, above dialogs");
        }

        window.Close();
    }
}
