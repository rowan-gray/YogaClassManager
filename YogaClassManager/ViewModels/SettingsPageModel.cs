using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;
using YogaClassManager.Database;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    public partial class SettingsPageModel : ObservableObject
    {
        public SettingsPageModel(DatabaseManager databaseManager, PopupService popupService)
        {

            DbFilePath = Preferences.Default.Get("DbFilePath", default(string));
            ChooseFileCommand = new Command(ChooseFile);
            CreateFileCommand = new Command(CreateFile);
            this.databaseManager = databaseManager;
            this.popupService = popupService;
        }

        private async void CreateFile()
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync(new CancellationToken());

                if (result.IsSuccessful)
                {
                    var folderPath = await popupService.DisplayPromptAsync("Specify folder", "Please enter the folder where you'd like the database to be created", "Continue", "Cancel");

                    if (folderPath == null)
                    {
                        await popupService.DisplayAlert("Invalid Location", "Invalid location chosen", "Ok");
                        return;
                    }


                    var filePath = folderPath + @"\yogamanager.db";

                    if (File.Exists(filePath))
                    {
                        await popupService.DisplayAlert("Invalid Location", "A database already exists in that location", "Ok");
                        return;
                    }

                    //SQLiteConnection.CreateFile(filePath);

                    DbFilePath = filePath;
                    Preferences.Default.Set("DbFilePath", filePath);
                    databaseManager.SetFilePath(filePath);

                    //await databaseManager.CreateDbAsync();
                }
            }
            catch { }
        }

        [ObservableProperty]
        private string dbFilePath;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public Command ChooseFileCommand { get; init; }
        public Command CreateFileCommand { get; init; }

        private async void ChooseFile()
        {
            try
            {
                //var databaseFileType = new FilePickerFileType(
                //    new Dictionary<DevicePlatform, IEnumerable<string>>
                //{
                //    { DevicePlatform.iOS, new[] { ".db" } }, // UTType values
                //    { DevicePlatform.Android, new[] { ".db" } }, // MIME type
                //    { DevicePlatform.WinUI, new[] { ".db" } }, // file extension
                //    { DevicePlatform.Tizen, new[] { ".db" } },
                //    { DevicePlatform.macOS, new[] { "public.database" } }, // UTType values
                //});

                PickOptions options = new()
                {
                    PickerTitle = "Please select the database"
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result is null)
                {
                    return;
                }

                var filePath = result.FullPath;

                if (!databaseManager.ValidateFilePath(filePath))
                {
                    await popupService.DisplayAlert("Invalid file", "You selected an invalid file.", "Okay");
                    return;
                }

                DbFilePath = filePath;
                Preferences.Default.Set("DbFilePath", filePath);
                databaseManager.SetFilePath(filePath);
            }
            catch
            {
                // user cancelled
            }

        }
    }
}
