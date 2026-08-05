using System.Collections.ObjectModel;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Passes;

/// <summary>
///     Shows the classes a single pass has been used for. Unlike other routed ViewModels this is not
///     Splat-registered - it always needs a specific Pass instance to display, so StudentsViewModel
///     constructs it directly (same idiom as directly `new`-ing a dialog ViewModel with resolved
///     args) and pushes it via HostScreen.Router.Navigate.Execute. Back navigation is a plain
///     Router.NavigateBack, which pops back to the still-alive StudentsViewModel instance underneath
///     (it was never torn down), so that page's student selection is preserved automatically.
/// </summary>
public class PassDetailViewModel : ReactiveObject, IRoutableViewModel
{
    private readonly IPageNavigator navigator;
    private readonly IToastService toastService;

    public PassDetailViewModel(IScreen hostScreen, Pass pass, IPassRepository passRepository,
        IPageNavigator navigator, IToastService toastService)
    {
        HostScreen = hostScreen;
        this.navigator = navigator;
        this.toastService = toastService;
        Pass = pass;

        ViewClassCommand = ReactiveCommand.CreateFromObservable<ClassAttendanceRecord, IRoutableViewModel>(ViewClass);

        LoadUsageHistorySafely(passRepository);
    }

    public string UrlPathSegment => "pass-detail";
    public IScreen HostScreen { get; }

    public Pass Pass { get; }

    public ObservableCollection<ClassAttendanceRecord> UsageHistory { get; } = new();

    public ReactiveCommand<ClassAttendanceRecord, IRoutableViewModel> ViewClassCommand { get; }

    /// <summary>A bare <c>_ = LoadAsync()</c> would discard any failure as an unobserved Task exception,
    /// leaving a permanently empty history with no explanation (UI_REVIEW.md A3).</summary>
    private async void LoadUsageHistorySafely(IPassRepository passRepository)
    {
        try
        {
            var history = await passRepository.GetUsageHistoryAsync(Pass.Id);
            foreach (var record in history)
                UsageHistory.Add(record);
        }
        catch (Exception exception)
        {
            toastService.ShowError($"Couldn't load this pass's usage history: {exception.Message}");
        }
    }

    private IObservable<IRoutableViewModel> ViewClass(ClassAttendanceRecord record) =>
        navigator.ToClassRoll(record.ClassRollId);
}
