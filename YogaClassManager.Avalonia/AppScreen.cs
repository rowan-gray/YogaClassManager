using ReactiveUI;

namespace YogaClassManager.Avalonia;

/// <summary>
///     The single IScreen the app routes through. Registered as a Splat singleton and used as the
///     DataContext for MainWindow's RoutedViewHost - replaces MAUI Shell + NavigationService entirely.
/// </summary>
public class AppScreen : ReactiveObject, IScreen
{
    public RoutingState Router { get; } = new();

    /// <summary>
    ///     Set by a page before navigating to Students (e.g. IdentitiesViewModel's "view student"
    ///     link) so the freshly-constructed StudentsViewModel can select that row once its own
    ///     RefreshCommand finishes loading - page ViewModels are recreated on every navigation
    ///     (Splat Register, not RegisterLazySingleton), so there's no other way to carry "select this
    ///     row" through Router.Navigate.Execute(viewModel).
    /// </summary>
    public int? PendingStudentSelectionId { get; set; }

    /// <summary>
    ///     Same purpose as <see cref="PendingStudentSelectionId" />, in the reverse direction: set by
    ///     StudentsViewModel's "view identity record" link before navigating to Identities, so the
    ///     freshly-constructed IdentitiesViewModel can select that row once its own RefreshCommand
    ///     finishes loading.
    /// </summary>
    public int? PendingIdentitySelectionId { get; set; }

    /// <summary>
    ///     Same purpose as <see cref="PendingStudentSelectionId" />, set by a "view class" link before
    ///     navigating to Class Schedules, so the freshly-constructed ClassSchedulesViewModel can select
    ///     that ClassSchedule once its own RefreshCommand finishes loading. Currently has no active
    ///     setter - kept for symmetry/possible future use, since today's "view class" links (Student
    ///     attendance history, Pass usage history) navigate to Class Rolls instead, where they only know
    ///     (and only need) the specific ClassRoll occurrence, not its parent ClassSchedule.
    /// </summary>
    public int? PendingClassScheduleSelectionId { get; set; }

    /// <summary>
    ///     Set by a "view class" link that knows a specific ClassRoll occurrence (not just its parent
    ///     ClassSchedule) - e.g. a student's attendance history, or a pass's usage history - before
    ///     navigating to Class Rolls. ClassRollsViewModel opens that roll's MarkRollViewModel dialog
    ///     once its own RefreshCommand finishes loading, the same dialog its own "Edit roll" action
    ///     already uses.
    /// </summary>
    public int? PendingClassRollIdToView { get; set; }
}
