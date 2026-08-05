using ReactiveUI;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     Cross-page navigation ("view this student's identity record", "open the roll this pass was used
///     for") plus the hand-off channel that carries "and select this row when you get there".
///
///     Page ViewModels used to do both jobs inline: <c>Locator.Current.GetService&lt;OtherPageVM&gt;()</c>
///     in a command body, and an unchecked <c>(AppScreen)hostScreen</c> downcast to reach the pending-
///     selection fields. That made StudentsViewModel and IdentitiesViewModel mutually type-coupled, and
///     made every one of those ViewModels untestable without a globally-populated Splat container and
///     the real AppScreen (UI_REVIEW.md A5). Behind this interface they depend on a navigation
///     capability instead of on each other.
///
///     The hand-off exists because page ViewModels are transient - a fresh instance is constructed on
///     every navigation - so "select row 7" cannot simply be set on the target ViewModel before
///     navigating to it. Each Take* reads the pending value and clears it, so it applies exactly once.
/// </summary>
public interface IPageNavigator
{
    IObservable<IRoutableViewModel> ToStudent(int studentId);

    IObservable<IRoutableViewModel> ToIdentity(int identityId);

    IObservable<IRoutableViewModel> ToClassRoll(int classRollId);

    int? TakePendingStudentSelection();

    int? TakePendingIdentitySelection();

    /// <summary>No producer today - nothing navigates to Class Schedules asking for a specific row
    /// (the "view class" links all know a ClassRoll occurrence, so they use
    /// <see cref="ToClassRoll" />). Kept so ClassSchedulesViewModel's consumer stays symmetrical with
    /// the other pages rather than being the one page that reads the channel differently; see
    /// UI_REVIEW.md U9 for the navigation-model decision this is waiting on.</summary>
    int? TakePendingClassScheduleSelection();

    int? TakePendingClassRollToView();
}
