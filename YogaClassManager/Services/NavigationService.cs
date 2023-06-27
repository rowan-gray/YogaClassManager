#nullable enable
using Microsoft.Maui.Controls.Platform.Compatibility;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.Classes;
using YogaClassManager.Models.People;
using YogaClassManager.ViewModels;
using YogaClassManager.Models;

namespace YogaClassManager.Services
{
    internal class NavigationService
    {
        public static async void NavigateTo(string pageRoute)
        {
            await Shell.Current.GoToAsync(pageRoute);
        }

        public static async Task NavigateTo(string pageRoute, params object[] parameters)
        {
            var navigationParameters = new Dictionary<string, object>();

            var parameterCount = 1;
            foreach (var parameter in parameters)
            {
                navigationParameters.Add($"parameter{parameterCount}", new Message(parameter));
                parameterCount++;
            }

            await Shell.Current.GoToAsync(pageRoute, navigationParameters);
        }

        private static async Task NavigateToAsync(string pageRoute, Dictionary<string, object> parameters)
        {
            await Shell.Current.GoToAsync(pageRoute, parameters);
        }

        public static async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        public static async Task NavigateToAddPassPage(Student student,
            Action? voidReturn = null,
            Action<Pass>? passReturn = null,
            ClassSchedule? classRestriction = null,
            Type? passType = null,
            bool? saveToDatabase = null)
        {
            if (student is null)
                return;

            var parameters = new Dictionary<string, object>();

            parameters.Add("student", new Message(student));

            if (voidReturn is not null)
            {
                parameters.Add("voidReturn", new Message(voidReturn));
            }
            if (passReturn is not null)
            {
                parameters.Add("passReturn", new Message(passReturn));
            }
            if (classRestriction is not null)
            {
                parameters.Add("class", new Message(classRestriction));
            }
            if (passType is not null)
            {
                parameters.Add("defaultPassType", new Message(passType));
            }
            if (saveToDatabase is not null)
            {
                parameters.Add("saveToDatabase", new Message(saveToDatabase));
            }

            await NavigateToAsync(nameof(AddPassPageModel), parameters);
        }

        public static async Task NavigateToEditPassPage(Pass pass, Action? voidCallback = null, Action<Pass>? passCallback = null, bool? saveToDatabase = null)
        {
            var parameters = new Dictionary<string, object>();

            if (pass is null)
                return;

            parameters.Add("pass", new Message(pass));

            if (voidCallback is not null)
            {
                parameters.Add("voidCallback", new Message(voidCallback));
            }
            if (passCallback is not null)
            {
                parameters.Add("passCallback", new Message(passCallback));
            }
            if (saveToDatabase is not null)
            {
                parameters.Add("saveToDatabase", new Message(saveToDatabase));
            }

            await NavigateToAsync(nameof(EditPassPageModel), parameters);
        }

        public static async Task NavigateToAddStudentPage(Action<int>? idReturn = null, Action<Student>? studentReturn = null)
        {
            var parameters = new Dictionary<string, object>();

            if (idReturn is not null)
            {
                parameters.Add("idReturn", new Message(idReturn));
            }
            if (studentReturn is not null)
            {
                parameters.Add("studentReturn", new Message(studentReturn));
            }

            await NavigateToAsync(nameof(AddStudentPageModel), parameters);
        }

        public static async Task NavigateToAddEmergencyContactPage(Student student, Action? voidCallback = null, Action<EmergencyContact>? emergencyContactCallback = null, bool? saveToDatabase = null)
        {
            var parameters = new Dictionary<string, object>();

            if (student is null)
                return;

            parameters.Add("student", new Message(student));

            if (voidCallback is not null)
            {
                parameters.Add("voidCallback", new Message(voidCallback));
            }
            if (emergencyContactCallback is not null)
            {
                parameters.Add("emergencyContactCallback", new Message(emergencyContactCallback));
            }
            if (saveToDatabase is not null)
            {
                parameters.Add("saveToDatabase", new Message(saveToDatabase));
            }

            await NavigateToAsync(nameof(AddEmergencyContactPageModel), parameters);
        }

        public static async Task NavigateToEditEmergencyContactPage(EmergencyContact emergencyContact, Action? voidCallback = null, Action<EmergencyContact>? emergencyContactCallback = null, bool? saveToDatabase = null)
        {
            var parameters = new Dictionary<string, object>();

            if (emergencyContact is null)
                return;

            parameters.Add("emergencyContact", new Message(emergencyContact));

            if (voidCallback is not null)
            {
                parameters.Add("voidCallback", new Message(voidCallback));
            }
            if (emergencyContactCallback is not null)
            {
                parameters.Add("emergencyContactCallback", new Message(emergencyContactCallback));
            }
            if (saveToDatabase is not null)
            {
                parameters.Add("saveToDatabase", new Message(saveToDatabase));
            }

            await NavigateToAsync(nameof(EditEmergencyContactPageModel), parameters);
        }

        public static async Task NavigateToAddPersonPage(Action<int>? idReturn = null, Action<Person>? personReturn = null, bool saveToDatabase = true)
        {
            var parameters = new Dictionary<string, object>();

            if (idReturn is not null)
            {
                parameters.Add("idCallback", new Message(idReturn));
            }
            if (personReturn is not null)
            {
                parameters.Add("personCallback", new Message(personReturn));
            }

            parameters.Add("saveToDatabase", new Message(saveToDatabase));


            await NavigateToAsync(nameof(AddPersonPageModel), parameters);
        }

        internal async static Task NavigateToEditClassSchedulePage(ClassSchedule classSchedule)
        {
            var parameters = new Dictionary<string, object>();

            if (classSchedule is null)
                return;

            parameters.Add("classSchedule", new Message(classSchedule));


            await NavigateToAsync(nameof(EditClassDetailsPageModel), parameters);
        }

        internal async static Task NavigateToEditTermPage(Term term)
        {
            var parameters = new Dictionary<string, object>();

            if (term is null)
                return;

            parameters.Add("term", new Message(term));


            await NavigateToAsync(nameof(EditTermPageModel), parameters);
        }

        internal static void NavigateToSettingsPage()
        {
            NavigateTo("///Settings");
        }

        internal async static Task NavigateToStudentPassesPage(Student student)
        {
            var parameters = new Dictionary<string, object>();

            if (student is null)
                return;

            parameters.Add("student", new Message(student));

            await NavigateToAsync(nameof(StudentPassesPageModel), parameters);
        }
    }
}
