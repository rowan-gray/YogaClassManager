using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
#if DEBUG
using Avalonia.Diagnostics;
#endif
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Splat;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.Services.Database;
using YogaClassManager.Avalonia.ViewModels.Bootstrap;
using YogaClassManager.Avalonia.ViewModels.ClassRolls;
using YogaClassManager.Avalonia.ViewModels.ClassSchedules;
using YogaClassManager.Avalonia.ViewModels.Dashboard;
using YogaClassManager.Avalonia.ViewModels.Identities;
using YogaClassManager.Avalonia.ViewModels.Passes;
using YogaClassManager.Avalonia.ViewModels.Settings;
using YogaClassManager.Avalonia.ViewModels.Students;
using YogaClassManager.Avalonia.ViewModels.Terms;
using YogaClassManager.Avalonia.Views;
using YogaClassManager.Avalonia.Views.Bootstrap;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Repositories;
using ClassRollsView = YogaClassManager.Avalonia.Views.ClassRolls.ClassRollsView;
using ClassSchedulesView = YogaClassManager.Avalonia.Views.ClassSchedules.ClassSchedulesView;
using DashboardView = YogaClassManager.Avalonia.Views.Dashboard.DashboardView;
using IdentitiesView = YogaClassManager.Avalonia.Views.Identities.IdentitiesView;
using PassDetailView = YogaClassManager.Avalonia.Views.Passes.PassDetailView;
using SettingsView = YogaClassManager.Avalonia.Views.Settings.SettingsView;
using StudentsView = YogaClassManager.Avalonia.Views.Students.StudentsView;
using TermsView = YogaClassManager.Avalonia.Views.Terms.TermsView;

namespace YogaClassManager.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
#if DEBUG
            this.AttachDevTools();
#endif
            // The startup gate runs before MainWindow (and therefore before any page/repository) is
            // touched: no database file is resolved yet, so nothing that depends on one can safely be
            // constructed. The gate becomes the temporary MainWindow itself (so the desktop lifetime
            // doesn't consider the app "done" the instant this method returns), and OnDatabaseReady
            // swaps in the real MainWindow once IAppDataStoreProvider has a bound store.
            var gateViewModel = Locator.Current.GetService<DatabaseGateViewModel>()!;
            var gateWindow = new DatabaseGateWindow { DataContext = gateViewModel };

            gateViewModel.Completed += (_, _) => OnDatabaseReady(desktop, gateWindow);
            gateWindow.Opened += async (_, _) => await gateViewModel.RunAsync();

            desktop.MainWindow = gateWindow;

            // Default is OnLastWindowClose - with a Mark Roll window able to stay open independently
            // (see IRollWindowService), that would leave the app running headless (no nav rail, no way
            // back) if the user closes MainWindow while one is still open. Closing MainWindow should
            // always mean "close the app".
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnDatabaseReady(IClassicDesktopStyleApplicationLifetime desktop, DatabaseGateWindow gateWindow)
    {
        var mainWindowViewModel = Locator.Current.GetService<MainWindowViewModel>()!;
        var mainWindow = new MainWindow { DataContext = mainWindowViewModel };

        // Reassign desktop.MainWindow to the REAL window *before* force-closing the gate window -
        // ShutdownMode.OnMainWindowClose tracks whichever window is currently desktop.MainWindow;
        // closing the gate while it's still the tracked MainWindow would shut the whole app down
        // instead of handing off to the real window.
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        gateWindow.CloseForced();
    }

    private static void ConfigureServices()
    {
        var locator = Locator.CurrentMutable;

        locator.RegisterLazySingleton<IAppSettingsService>(() => new AppSettingsService());
        locator.RegisterLazySingleton<IDatabaseProvisioningService>(() => new DatabaseProvisioningService());
        locator.RegisterLazySingleton<IAppDataStoreProvider>(() =>
            new AppDataStoreProvider(Locator.Current.GetService<IDatabaseProvisioningService>()!));
        locator.RegisterLazySingleton<IFilePickerService>(() => new FilePickerService());

        // Each SqliteXxxRepository takes IDataStoreProvider and resolves the current store fresh on
        // every operation rather than caching it - so these registrations, resolved once here, keep
        // working transparently after IAppDataStoreProvider.SwapToAsync rebinds to a different
        // database (see the Settings page's "change database file" flow) with no re-registration
        // needed and no already-injected ViewModel reference going stale.
        locator.RegisterLazySingleton<IIdentityRepository>(() =>
            new SqliteIdentityRepository(Locator.Current.GetService<IAppDataStoreProvider>()!));
        locator.RegisterLazySingleton<IPassRepository>(() =>
            new SqlitePassRepository(Locator.Current.GetService<IAppDataStoreProvider>()!));
        locator.RegisterLazySingleton<IEmergencyContactRepository>(() =>
            new SqliteEmergencyContactRepository(Locator.Current.GetService<IAppDataStoreProvider>()!));
        locator.RegisterLazySingleton<IStudentRepository>(() =>
            new SqliteStudentRepository(Locator.Current.GetService<IAppDataStoreProvider>()!,
                Locator.Current.GetService<IIdentityRepository>()!,
                Locator.Current.GetService<IPassRepository>()!,
                Locator.Current.GetService<IEmergencyContactRepository>()!));
        locator.RegisterLazySingleton<IClassScheduleRepository>(() =>
            new SqliteClassScheduleRepository(Locator.Current.GetService<IAppDataStoreProvider>()!));
        locator.RegisterLazySingleton<IClassRollRepository>(() =>
            new SqliteClassRollRepository(Locator.Current.GetService<IAppDataStoreProvider>()!));
        locator.RegisterLazySingleton<ITermRepository>(() =>
            new SqliteTermRepository(Locator.Current.GetService<IAppDataStoreProvider>()!));

        locator.RegisterLazySingleton<AppScreen>(() => new AppScreen());
        locator.RegisterLazySingleton<IDialogService>(() => new DialogService());
        locator.RegisterLazySingleton<IToastService>(() => new ToastService());
        RxApp.DefaultExceptionHandler = new ToastExceptionHandler(Locator.Current.GetService<IToastService>()!);

        // Resolves page ViewModels through Splat itself, which is what keeps that lookup - and the
        // page-to-page type coupling it caused - out of the ViewModels (see IPageNavigator).
        locator.RegisterLazySingleton<IPageNavigator>(() => new PageNavigator(
            Locator.Current.GetService<AppScreen>()!,
            type => Locator.Current.GetService(type)));

        locator.RegisterLazySingleton<IRollWindowService>(() => new RollWindowService(
            Locator.Current.GetService<IClassRollRepository>()!,
            Locator.Current.GetService<IStudentRepository>()!,
            Locator.Current.GetService<IPassRepository>()!,
            Locator.Current.GetService<ITermRepository>()!,
            Locator.Current.GetService<IToastService>()!));

        locator.RegisterLazySingleton<DatabaseGateViewModel>(() => new DatabaseGateViewModel(
            Locator.Current.GetService<IAppSettingsService>()!,
            Locator.Current.GetService<IAppDataStoreProvider>()!,
            Locator.Current.GetService<IFilePickerService>()!));

        locator.Register<DashboardViewModel>(() => new DashboardViewModel(
            Locator.Current.GetService<AppScreen>()!,
            Locator.Current.GetService<IStudentRepository>()!,
            Locator.Current.GetService<IClassScheduleRepository>()!,
            Locator.Current.GetService<IClassRollRepository>()!,
            Locator.Current.GetService<IRollWindowService>()!,
            Locator.Current.GetService<IToastService>()!));

        locator.Register<IdentitiesViewModel>(() => new IdentitiesViewModel(
            Locator.Current.GetService<AppScreen>()!,
            Locator.Current.GetService<IIdentityRepository>()!,
            Locator.Current.GetService<IStudentRepository>()!,
            Locator.Current.GetService<IEmergencyContactRepository>()!,
            Locator.Current.GetService<IDialogService>()!,
            Locator.Current.GetService<IToastService>()!,
            Locator.Current.GetService<IPageNavigator>()!));

        locator.Register<StudentsViewModel>(() => new StudentsViewModel(
            Locator.Current.GetService<AppScreen>()!,
            Locator.Current.GetService<IStudentRepository>()!,
            Locator.Current.GetService<IIdentityRepository>()!,
            Locator.Current.GetService<IPassRepository>()!,
            Locator.Current.GetService<ITermRepository>()!,
            Locator.Current.GetService<IClassRollRepository>()!,
            Locator.Current.GetService<IDialogService>()!,
            Locator.Current.GetService<IToastService>()!,
            Locator.Current.GetService<IPageNavigator>()!));

        locator.Register<ClassSchedulesViewModel>(() => new ClassSchedulesViewModel(
            Locator.Current.GetService<AppScreen>()!,
            Locator.Current.GetService<IClassScheduleRepository>()!,
            Locator.Current.GetService<IClassRollRepository>()!,
            Locator.Current.GetService<IDialogService>()!,
            Locator.Current.GetService<IToastService>()!,
            Locator.Current.GetService<IRollWindowService>()!,
            Locator.Current.GetService<IPageNavigator>()!));

        locator.Register<ClassRollsViewModel>(() => new ClassRollsViewModel(
            Locator.Current.GetService<AppScreen>()!,
            Locator.Current.GetService<IClassRollRepository>()!,
            Locator.Current.GetService<IDialogService>()!,
            Locator.Current.GetService<IToastService>()!,
            Locator.Current.GetService<IRollWindowService>()!,
            Locator.Current.GetService<IPageNavigator>()!));

        locator.Register<TermsViewModel>(() => new TermsViewModel(
            Locator.Current.GetService<AppScreen>()!,
            Locator.Current.GetService<ITermRepository>()!,
            Locator.Current.GetService<IClassScheduleRepository>()!,
            Locator.Current.GetService<IDialogService>()!,
            Locator.Current.GetService<IToastService>()!));

        locator.Register<SettingsViewModel>(() => new SettingsViewModel(
            Locator.Current.GetService<AppScreen>()!,
            Locator.Current.GetService<IAppSettingsService>()!,
            Locator.Current.GetService<IAppDataStoreProvider>()!,
            Locator.Current.GetService<IFilePickerService>()!,
            Locator.Current.GetService<IRollWindowService>()!,
            Locator.Current.GetService<IDialogService>()!,
            Locator.Current.GetService<IToastService>()!));

        locator.RegisterLazySingleton<MainWindowViewModel>(() =>
            new MainWindowViewModel(Locator.Current.GetService<AppScreen>()!,
                Locator.Current.GetService<IDialogService>()!));

        // RoutedViewHost resolves views through ReactiveUI's IViewLocator/Splat, not Avalonia's
        // DataTemplate system - every routed ViewModel needs its View registered as IViewFor<T> here.
        locator.Register<IViewFor<DashboardViewModel>>(() => new DashboardView());
        locator.Register<IViewFor<IdentitiesViewModel>>(() => new IdentitiesView());
        locator.Register<IViewFor<StudentsViewModel>>(() => new StudentsView());
        // PassDetailViewModel itself is never Splat-resolved (StudentsViewModel constructs it directly
        // with a specific Pass) - only its View needs registering, so RoutedViewHost can find it.
        locator.Register<IViewFor<PassDetailViewModel>>(() => new PassDetailView());
        locator.Register<IViewFor<ClassSchedulesViewModel>>(() => new ClassSchedulesView());
        locator.Register<IViewFor<ClassRollsViewModel>>(() => new ClassRollsView());
        locator.Register<IViewFor<TermsViewModel>>(() => new TermsView());
        locator.Register<IViewFor<SettingsViewModel>>(() => new SettingsView());
    }
}
