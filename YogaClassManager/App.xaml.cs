namespace YogaClassManager;

using YogaClassManager.ViewModels;
using YogaClassManager.Views;
using YogaClassManager.Views.Classes;
using YogaClassManager.Views.EmergencyContacts;
using YogaClassManager.Views.Passes;
using YogaClassManager.Views.People;
using YogaClassManager.Views.Students;
using YogaClassManager.Views.Terms;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new AppShell();


        Routing.RegisterRoute(nameof(EditDetailsPage), typeof(EditDetailsPage));
        Routing.RegisterRoute(nameof(EditPassPageModel), typeof(EditPassPage));
        Routing.RegisterRoute(nameof(AddPassPageModel), typeof(AddPassPage));
        Routing.RegisterRoute(nameof(AddEmergencyContactPageModel), typeof(AddEmergencyContactPage));
        Routing.RegisterRoute(nameof(EditEmergencyContactPageModel), typeof(EditEmergencyContactPage));
        Routing.RegisterRoute(nameof(AddPersonPageModel), typeof(AddPersonPage));
        Routing.RegisterRoute(nameof(EditClassDetailsPageModel), typeof(EditClassDetailsPage));
        Routing.RegisterRoute(nameof(AddClassPageModel), typeof(AddClassPage));
        Routing.RegisterRoute(nameof(EditTermPageModel), typeof(EditTermPage));
        Routing.RegisterRoute(nameof(AddTermPageModel), typeof(AddTermPage));
        Routing.RegisterRoute(nameof(SelectClassPageModel), typeof(SelectClassPage));
        Routing.RegisterRoute(nameof(MarkRollPageModel), typeof(MarkRollPage));
        Routing.RegisterRoute(nameof(SearchPeoplePageModel), typeof(SearchPeoplePage));
        Routing.RegisterRoute(nameof(AddStudentPageModel), typeof(AddStudentPage));
        Routing.RegisterRoute(nameof(StudentPassesPageModel), typeof(StudentPassesPage));
    }
}

