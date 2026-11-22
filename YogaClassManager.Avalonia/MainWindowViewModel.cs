using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls;
using Material.Icons;
using ReactiveUI;
using Splat;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.ClassRolls;
using YogaClassManager.Avalonia.ViewModels.ClassSchedules;
using YogaClassManager.Avalonia.ViewModels.Dashboard;
using YogaClassManager.Avalonia.ViewModels.Identities;
using YogaClassManager.Avalonia.ViewModels.Settings;
using YogaClassManager.Avalonia.ViewModels.Students;
using YogaClassManager.Avalonia.ViewModels.Terms;

namespace YogaClassManager.Avalonia;

/// <summary>
///     Wraps AppScreen with the nav-rail items MainWindow's collapsible SplitView binds to.
///     Replaces MAUI's AppShell FlyoutItem/ShellContent routes.
/// </summary>
public class MainWindowViewModel : ReactiveObject, IScreen
{
    /// <summary>Below this window width the side menu is forced to its collapsed icon rail (see
    /// <see cref="IsNarrow" />).</summary>
    public const double NarrowWindowWidth = 900;

    private readonly AppScreen appScreen;
    private bool isPaneOpen = true;
    private bool isNarrow;

    /// <summary>Whether the menu should be open while the window is wide - the user's own choice, kept
    /// so that narrowing the window (which forces it closed) and widening it again restores it.</summary>
    private bool openWhenWide = true;

    public MainWindowViewModel(AppScreen appScreen, IDialogService dialogService)
    {
        this.appScreen = appScreen;
        Dialogs = dialogService;

        NavItems = new ObservableCollection<NavItemViewModel>
        {
            CreateNavItem("Dashboard", MaterialIconKind.ViewDashboardOutline, "dashboard",
                () => Locator.Current.GetService<DashboardViewModel>()!),
            CreateNavItem("Identities", MaterialIconKind.AccountGroupOutline, "identities",
                () => Locator.Current.GetService<IdentitiesViewModel>()!),
            CreateNavItem("Students", MaterialIconKind.AccountSchoolOutline, "students",
                () => Locator.Current.GetService<StudentsViewModel>()!),
            CreateNavItem("Class Schedules", MaterialIconKind.GoogleClassroom, "classes",
                () => Locator.Current.GetService<ClassSchedulesViewModel>()!),
            CreateNavItem("Class Rolls", MaterialIconKind.ClipboardCheckOutline, "class-rolls",
                () => Locator.Current.GetService<ClassRollsViewModel>()!),
            CreateNavItem("Terms", MaterialIconKind.CalendarMonthOutline, "terms",
                () => Locator.Current.GetService<TermsViewModel>()!)
        };

        SettingsNavItem = CreateNavItem("Settings", MaterialIconKind.CogOutline, "settings",
            () => Locator.Current.GetService<SettingsViewModel>()!);

        ToggleNavCommand = ReactiveCommand.Create(() =>
        {
            IsPaneOpen = !IsPaneOpen;
            if (!IsNarrow)
                openWhenWide = IsPaneOpen;
        });

        Router.CurrentViewModel.Subscribe(active => UpdateSelection(active?.UrlPathSegment));

        NavItems[0].Command.Execute().Subscribe();
    }

    public RoutingState Router => appScreen.Router;

    public IDialogService Dialogs { get; }

    public ObservableCollection<NavItemViewModel> NavItems { get; }
    public NavItemViewModel SettingsNavItem { get; }

    public ReactiveCommand<Unit, Unit> ToggleNavCommand { get; }

    public bool IsPaneOpen
    {
        get => isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref isPaneOpen, value);
    }

    /// <summary>
    ///     Set by MainWindow from its width (below <see cref="NarrowWindowWidth" />). Narrow forces the
    ///     menu closed to its icon rail, and opening it with the menu button then overlays the page
    ///     (<see cref="PaneDisplayMode" />) instead of squeezing it; picking a page closes it again.
    ///     Widening the window restores the menu to how it was last left while wide, inline.
    /// </summary>
    public bool IsNarrow
    {
        get => isNarrow;
        set
        {
            if (isNarrow == value)
                return;

            this.RaiseAndSetIfChanged(ref isNarrow, value);
            this.RaisePropertyChanged(nameof(PaneDisplayMode));
            IsPaneOpen = !value && openWhenWide;
        }
    }

    public SplitViewDisplayMode PaneDisplayMode =>
        IsNarrow ? SplitViewDisplayMode.CompactOverlay : SplitViewDisplayMode.CompactInline;

    private NavItemViewModel CreateNavItem(string label, MaterialIconKind icon, string urlPathSegment,
        Func<IRoutableViewModel> resolveViewModel)
    {
        var command = ReactiveCommand.CreateFromObservable(() =>
        {
            // In the narrow layout the open menu overlays the page - close it once a page is chosen.
            if (IsNarrow)
                IsPaneOpen = false;
            return Router.Navigate.Execute(resolveViewModel());
        });
        return new NavItemViewModel(label, icon, command, urlPathSegment);
    }

    private void UpdateSelection(string? activeUrlPathSegment)
    {
        foreach (var item in NavItems)
            item.IsSelected = item.UrlPathSegment == activeUrlPathSegment;

        SettingsNavItem.IsSelected = SettingsNavItem.UrlPathSegment == activeUrlPathSegment;
    }
}
