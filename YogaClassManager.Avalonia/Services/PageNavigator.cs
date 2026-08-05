using ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.ClassRolls;
using YogaClassManager.Avalonia.ViewModels.Identities;
using YogaClassManager.Avalonia.ViewModels.Students;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     The one place that knows both how to resolve a page ViewModel and how the pending-selection
///     hand-off is stored. Resolving pages from the service locator is this class's job - it is a
///     navigation service, not a ViewModel - which is exactly what keeps that lookup (and the resulting
///     page-to-page type coupling) out of the ViewModels themselves.
///
///     Page ViewModels are registered transient, so each Resolve here builds a fresh instance, matching
///     what Router.Navigate expects.
/// </summary>
public class PageNavigator : IPageNavigator
{
    private readonly AppScreen screen;
    private readonly Func<Type, object?> resolve;

    public PageNavigator(AppScreen screen, Func<Type, object?> resolve)
    {
        this.screen = screen;
        this.resolve = resolve;
    }

    public IObservable<IRoutableViewModel> ToStudent(int studentId)
    {
        screen.PendingStudentSelectionId = studentId;
        return Navigate<StudentsViewModel>();
    }

    public IObservable<IRoutableViewModel> ToIdentity(int identityId)
    {
        screen.PendingIdentitySelectionId = identityId;
        return Navigate<IdentitiesViewModel>();
    }

    public IObservable<IRoutableViewModel> ToClassRoll(int classRollId)
    {
        screen.PendingClassRollIdToView = classRollId;
        return Navigate<ClassRollsViewModel>();
    }

    public int? TakePendingStudentSelection()
    {
        var pending = screen.PendingStudentSelectionId;
        screen.PendingStudentSelectionId = null;
        return pending;
    }

    public int? TakePendingIdentitySelection()
    {
        var pending = screen.PendingIdentitySelectionId;
        screen.PendingIdentitySelectionId = null;
        return pending;
    }

    public int? TakePendingClassScheduleSelection()
    {
        var pending = screen.PendingClassScheduleSelectionId;
        screen.PendingClassScheduleSelectionId = null;
        return pending;
    }

    public int? TakePendingClassRollToView()
    {
        var pending = screen.PendingClassRollIdToView;
        screen.PendingClassRollIdToView = null;
        return pending;
    }

    private IObservable<IRoutableViewModel> Navigate<TViewModel>() where TViewModel : IRoutableViewModel
    {
        var viewModel = (TViewModel?)resolve(typeof(TViewModel))
                        ?? throw new InvalidOperationException(
                            $"{typeof(TViewModel).Name} is not registered - see App.axaml.cs's ConfigureServices.");

        return screen.Router.Navigate.Execute(viewModel);
    }
}
