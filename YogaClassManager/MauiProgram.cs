using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using YogaClassManager.Database;
using YogaClassManager.Services;
using YogaClassManager.ViewModels;
using YogaClassManager.Views;
using YogaClassManager.Views.Classes;
using YogaClassManager.Views.EmergencyContacts;
using YogaClassManager.Views.Passes;
using YogaClassManager.Views.People;
using YogaClassManager.Views.Students;
using YogaClassManager.Views.Terms;

namespace YogaClassManager;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
        var dbFilePath = Preferences.Default.Get("DbFilePath", default(string));;

        builder.Services.AddSingleton(new DatabaseManager(dbFilePath));
        builder.Services.AddSingleton<PopupService>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainPageModel>();
        builder.Services.AddTransient<StudentsPageModel>();
        builder.Services.AddTransient<StudentsPage>();
        builder.Services.AddTransient<AddStudentPageModel>();
        builder.Services.AddTransient<AddStudentPage>();
        builder.Services.AddTransient<ClassesPageModel>();
        builder.Services.AddTransient<ClassesPage>();
        builder.Services.AddTransient<TermsPageModel>();
        builder.Services.AddTransient<TermsPage>();
        builder.Services.AddTransient<EditDetailsPageModel>();
        builder.Services.AddTransient<EditDetailsPage>();
        builder.Services.AddTransient<EditPassPageModel>();
        builder.Services.AddTransient<EditPassPage>();
        builder.Services.AddTransient<AddPassPageModel>();
        builder.Services.AddTransient<AddPassPage>();
        builder.Services.AddTransient<EditEmergencyContactPageModel>();
        builder.Services.AddTransient<EditEmergencyContactPage>();
        builder.Services.AddTransient<AddEmergencyContactPageModel>();
        builder.Services.AddTransient<AddEmergencyContactPage>();
        builder.Services.AddTransient<AddPersonPageModel>();
        builder.Services.AddTransient<AddPersonPage>();
        builder.Services.AddTransient<EditClassDetailsPageModel>();
        builder.Services.AddTransient<EditClassDetailsPage>();
        builder.Services.AddTransient<AddClassPageModel>();
        builder.Services.AddTransient<AddClassPage>();
        builder.Services.AddTransient<EditTermPageModel>();
        builder.Services.AddTransient<EditTermPage>();
        builder.Services.AddTransient<AddTermPageModel>();
        builder.Services.AddTransient<AddTermPage>();
        builder.Services.AddTransient<SelectClassPageModel>();
        builder.Services.AddTransient<SelectClassPage>();
        builder.Services.AddTransient<MarkRollPageModel>();
        builder.Services.AddTransient<MarkRollPage>();
        builder.Services.AddTransient<SearchPeoplePageModel>();
        builder.Services.AddTransient<SearchPeoplePage>();
        builder.Services.AddTransient<PeoplePageModel>();
        builder.Services.AddTransient<PeoplePage>();
        builder.Services.AddTransient<SettingsPageModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<StudentPassesPageModel>();
        builder.Services.AddTransient<StudentPassesPage>();

        builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

