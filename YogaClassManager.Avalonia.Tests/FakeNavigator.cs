using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Records cross-page navigation instead of performing it, and hands out scripted pending
///     selections. Before IPageNavigator existed, every page ViewModel reached the same state through
///     an unchecked <c>(AppScreen)hostScreen</c> downcast and a <c>Locator.Current</c> lookup, so this
///     double could not exist at all (UI_REVIEW.md A5) - a mock IScreen threw InvalidCastException, and
///     the lookup needed a globally-populated Splat container.
/// </summary>
public class FakeNavigator : IPageNavigator
{
    /// <summary>Every cross-page navigation asked for, in order, as (page, record id).</summary>
    public List<(string Page, int Id)> Navigations { get; } = [];

    public int? PendingStudentSelection { get; set; }
    public int? PendingIdentitySelection { get; set; }
    public int? PendingClassScheduleSelection { get; set; }
    public int? PendingClassRollToView { get; set; }

    public IObservable<IRoutableViewModel> ToStudent(int studentId) => Record("student", studentId);

    public IObservable<IRoutableViewModel> ToIdentity(int identityId) => Record("identity", identityId);

    public IObservable<IRoutableViewModel> ToClassRoll(int classRollId) => Record("class-roll", classRollId);

    public int? TakePendingStudentSelection()
    {
        var pending = PendingStudentSelection;
        PendingStudentSelection = null;
        return pending;
    }

    public int? TakePendingIdentitySelection()
    {
        var pending = PendingIdentitySelection;
        PendingIdentitySelection = null;
        return pending;
    }

    public int? TakePendingClassScheduleSelection()
    {
        var pending = PendingClassScheduleSelection;
        PendingClassScheduleSelection = null;
        return pending;
    }

    public int? TakePendingClassRollToView()
    {
        var pending = PendingClassRollToView;
        PendingClassRollToView = null;
        return pending;
    }

    /// <summary>Returns a value rather than an empty sequence: ReactiveCommand.Execute() is awaited by
    /// callers, and awaiting an observable that completes without producing anything throws.</summary>
    private IObservable<IRoutableViewModel> Record(string page, int id)
    {
        Navigations.Add((page, id));
        return Observable.Return<IRoutableViewModel>(new NavigatedTo(page));
    }

    private class NavigatedTo(string page) : ReactiveObject, IRoutableViewModel
    {
        public string UrlPathSegment => page;
        public IScreen HostScreen { get; } = null!;
    }
}
