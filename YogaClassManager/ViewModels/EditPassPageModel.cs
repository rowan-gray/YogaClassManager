using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Passes;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(PassParameter), "pass")]
    [QueryProperty(nameof(VoidCallbackParameter), "voidCallback")]
    [QueryProperty(nameof(PassCallbackParameter), "passCallback")]
    [QueryProperty(nameof(SaveToDatabaseParameter), "saveToDatabase")]
    public partial class EditPassPageModel : ObservableObject
    {
        [ObservableProperty]
        private Pass editingPass;
        [ObservableProperty]
        private PassAlteration newAlteration;
        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public Message PassParameter
        {
            set
            {
                if (value.Parameter.GetType() == typeof(DatedPass))
                    EditingPass = (DatedPass)value.Parameter;
                else if (value.Parameter.GetType() == typeof(TermPass))
                    EditingPass = (TermPass)value.Parameter;
                else if (value.Parameter.GetType() == typeof(CasualPass))
                    EditingPass = (CasualPass)value.Parameter;
                else
                    EditingPass = (Pass)value.Parameter;
                NewAlteration = new(-1, EditingPass.Id, 0, null);

                UpdateCanSaveExecuteCommandExecute(null);
                UpdateCanAddAlterationExecuteCommandExecute(null);

                SaveToDatabase = true;
            }
        }
        public Message VoidCallbackParameter { set => VoidCallback = (Action)value.Parameter; }

        public Action VoidCallback { get; set; }

        public Message PassCallbackParameter { set => PassCallback = (Action<Pass>)value.Parameter; }

        public Action<Pass> PassCallback { get; set; }

        public Message SaveToDatabaseParameter { set => SaveToDatabase = (bool)value.Parameter; }

        public bool SaveToDatabase { get; set; }

        public Command SaveCommand { get; init; }

        public Command CancelCommand { get; init; }
        public Command UpdateCanSaveExecuteCommand { get; init; }
        public Command AddAlterationCommand { get; init; }
        public Command RemoveAlterationCommand { get; init; }
        public Command UpdateCanAddAlterationExecuteCommand { get; init; }

        public EditPassPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            SaveCommand = new Command(SaveCommandExecute, SaveCommandCanExecute);
            CancelCommand = new Command(CancelCommandExecute);
            UpdateCanSaveExecuteCommand = new Command(UpdateCanSaveExecuteCommandExecute);
            AddAlterationCommand = new Command(AddAlterationCommandExecute, AddAlterationCommandCanExecute);
            RemoveAlterationCommand = new Command(RemoveAlterationCommandExecute);
            UpdateCanAddAlterationExecuteCommand = new Command(UpdateCanAddAlterationExecuteCommandExecute);
        }

        private void UpdateCanSaveExecuteCommandExecute(object obj)
        {
            SaveCommand.ChangeCanExecute();
        }

        private bool SaveCommandCanExecute()
        {
            return EditingPass is not null && EditingPass.IsValid();
        }

        private void UpdateCanAddAlterationExecuteCommandExecute(object obj)
        {
            AddAlterationCommand.ChangeCanExecute();
        }

        private bool AddAlterationCommandCanExecute(object arg)
        {
            return NewAlteration is not null && NewAlteration.IsValid();
        }

        private async void RemoveAlterationCommandExecute(object obj)
        {
            if (obj is null)
                return;
            if (obj.GetType() != typeof(PassAlteration))
                return;

            var passAlteration = (PassAlteration)obj;

            if (await Shell.Current.DisplayAlert("Confirm", "Are you sure you want to remove this alteration?", "Yes", "No"))
            {
                EditingPass.Alterations.Remove(passAlteration);
                SaveCommand?.ChangeCanExecute();
            }
        }

        private void AddAlterationCommandExecute(object obj)
        {
            EditingPass.Alterations.Add(NewAlteration);
            NewAlteration = new(-1, EditingPass.Id, 1, null);
            SaveCommand?.ChangeCanExecute();
        }

        public async void SaveCommandExecute()
        {
            bool success = true;

            if (SaveToDatabase)
            {
                if (await Shell.Current.DisplayAlert("Confirm", "Are you sure you want to save?", "Yes", "No"))
                {
                    success = await SavePassToDatabase(EditingPass);
                }
            }

            if (!success)
            {
                return;
            }

            await NavigationService.GoBackAsync();
            VoidCallback?.Invoke();
            PassCallback?.Invoke(EditingPass);
        }

        private async Task<bool> SavePassToDatabase(Pass pass)
        {
            try
            {
                await databaseManager.PassesService.SavePassAsync(CancellationToken.None, pass);
                return true;
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                return false;
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                return false;
            }
        }

        public async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }
    }
}
