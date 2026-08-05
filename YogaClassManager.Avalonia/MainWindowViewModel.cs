using System.Collections.ObjectModel;
using System.Reactive;
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
    private readonly AppScreen appScreen;
    private bool isPaneOpen = true;

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

        ToggleNavCommand = ReactiveCommand.Create(() => { IsPaneOpen = !IsPaneOpen; });

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

    private NavItemViewModel CreateNavItem(string label, MaterialIconKind icon, string urlPathSegment,
        Func<IRoutableViewModel> resolveViewModel)
    {
        var command = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(resolveViewModel()));
        return new NavItemViewModel(label, icon, command, urlPathSegment);
    }

    private void UpdateSelection(string? activeUrlPathSegment)
    {
        foreach (var item in NavItems)
            item.IsSelected = item.UrlPathSegment == activeUrlPathSegment;

        SettingsNavItem.IsSelected = SettingsNavItem.UrlPathSegment == activeUrlPathSegment;
    }
}
