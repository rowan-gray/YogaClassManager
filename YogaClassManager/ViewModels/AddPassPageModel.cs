using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Classes;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.People;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels
{
    [QueryProperty(nameof(StudentParameter), "student")]
    [QueryProperty(nameof(VoidCallbackParameter), "voidReturn")]
    [QueryProperty(nameof(PassCallbackParameter), "passReturn")]
    [QueryProperty(nameof(ClassParameter), "class")]
    [QueryProperty(nameof(DefaultPassTypeParameter), "defaultPassType")]
    [QueryProperty(nameof(SaveToDatabaseParameter), "saveToDatabase")]
    public partial class AddPassPageModel : ObservableObject
    {
        [ObservableProperty]
        private DatedPass datedPass;
        [ObservableProperty]
        private TermPass termPass;
        [ObservableProperty]
        private CasualPass casualPass;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentPass))]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string passType;
        [ObservableProperty]
        private ObservableCollection<Term> terms;
        [ObservableProperty]
        private ObservableCollection<string> passTypes;
        [ObservableProperty]
        private PassAlteration newAlteration;
        public Pass CurrentPass {
            get
            {
                switch (PassType)
                {
                    case "Dated Pass":
                        return DatedPass;
                    case "Term Pass":
                        return TermPass;
                    case "Casual Pass":
                        return CasualPass;
                    default:
                        return null;
                }
            }
        }

        private readonly DatabaseManager databaseManager;
        private readonly PopupService popupService;

        public AddPassPageModel(DatabaseManager databaseManager, PopupService popupService)
        {
            this.databaseManager = databaseManager;
            this.popupService = popupService;
            SaveCommand = new RelayCommand(SaveCommandExecute, SaveCommandCanExecute);
            CancelCommand = new Command(CancelCommandExecute);
            UpdateCanExecutesCommand = new Command(UpdateCanExecutesCommandExecute);
            TermChangedCommand = new Command(TermChanged);
            AppearingCommand = new Command(Appearing);
            PassTypes = new ObservableCollection<string>() { "Dated Pass", "Term Pass", "Casual Pass" };
            PassType = "Dated Pass";
            SaveToDatabase = true;
            AddAlterationCommand = new Command(AddAlterationCommandExecute, AddAlterationCommandCanExecute);
            RemoveAlterationCommand = new Command(RemoveAlterationCommandExecute);
            UpdateCanAddAlterationExecuteCommand = new Command(UpdateCanAddAlterationExecuteCommandExecute);
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
                CurrentPass.Alterations.Remove(passAlteration);
                SaveCommand?.NotifyCanExecuteChanged();
            }
        }

        private void AddAlterationCommandExecute(object obj)
        {
            CurrentPass.Alterations.Add(NewAlteration);
            NewAlteration = new(-1, CurrentPass.Id, 1, null);
            SaveCommand?.NotifyCanExecuteChanged();
        }

        private void TermChanged()
        {
            TermPass.TermClassSchedule = TermPass.Term.Classes.FirstOrDefault();
        }

        private bool SaveCommandCanExecute()
        {
            if (CurrentPass is null)
            {
                return false;
            }

            return CurrentPass.IsValid();
        }

        public Message VoidCallbackParameter { set => VoidCallback = (Action)value.Parameter; }

        public Action VoidCallback { get; set; }
        public Message PassCallbackParameter { set => PassCallback = (Action<Pass>)value.Parameter; }

        public Action<Pass> PassCallback { get; set; }
        public Message StudentParameter
        {
            set
            {
                Student = (Student)value.Parameter;
                NewAlteration = new(-1, -1, 0, null);
            }
        }
        [ObservableProperty]
        private Student student;
        public Message ClassParameter { set => ClassRestriction = (ClassSchedule)value.Parameter; }
        public ClassSchedule ClassRestriction
        {
            get; set;
        }
        public Message DefaultPassTypeParameter
        {
            set => defaultPassType = value.Parameter.ToString();
        }
        private string defaultPassType = "Term Pass";
        public Message SaveToDatabaseParameter { set => SaveToDatabase = (bool)value.Parameter; }
        public bool SaveToDatabase
        {
            get;
            set;
        }

        public RelayCommand SaveCommand { get; init; }
        public Command CancelCommand { get; init; }
        public Command TermChangedCommand { get; init; }
        public Command UpdateCanExecutesCommand { get; init; }
        public Command AppearingCommand { get; init; }
        public Command AddAlterationCommand { get; init; }
        public Command RemoveAlterationCommand { get; init; }
        public Command UpdateCanAddAlterationExecuteCommand { get; init; }

        public void UpdateCanExecutesCommandExecute()
        {
            SaveCommand?.NotifyCanExecuteChanged();
        }

        public async void SaveCommandExecute()
        {
            if (SaveToDatabase)
            {
                try
                {
                    await databaseManager.PassesService.AddPassAsync(CancellationToken.None, CurrentPass);
                }
                catch (TaskCanceledException)
                {
                    await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
                    return;
                }
                catch (Exception e)
                {
                    await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
                    return;
                }
            }

            await NavigationService.GoBackAsync();
            VoidCallback?.Invoke();
            PassCallback?.Invoke(CurrentPass);
        }

        public async void CancelCommandExecute()
        {
            await NavigationService.GoBackAsync();
        }

        private async void Appearing()
        {
            try
            {
                Terms = new(await databaseManager.TermsService.GetTermsAsync(CancellationToken.None));
            }
            catch (TaskCanceledException)
            {
                await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
            }
            catch (Exception e)
            {
                await popupService.DisplayAlert("Database error", $"There was an error while trying to access the database.\n{e.Message}", "Ok");
            }

            var pass = new Pass(-1, Student.Id, 0, new());

            for (int index = 0; index < Terms?.Count; index++)
            {
                Term term = Terms[index];
                if (term.Classes.Count == 0)
                {
                    // removes term from the list if there is no class
                    Terms.RemoveAt(index);
                    index--;
                    continue;
                }

                if (ClassRestriction is not null)
                {
                    if (term.Classes.FirstOrDefault(termClass => termClass.ClassSchedule.Id == ClassRestriction.Id) is null)
                    {
                        // removes term from the list if there is no class
                        Terms.RemoveAt(index);
                        index--;
                        continue;
                    }
                }
            }

            DatedPass = new DatedPass(pass, 10, DateOnly.FromDateTime(DateTime.Now), DateOnly.FromDateTime(DateTime.Now).AddMonths(3));
            CasualPass = new CasualPass(pass, 1);

            if (Terms?.Count > 0)
            {
                TermPass = new TermPass(pass, Terms.FirstOrDefault(), Terms.FirstOrDefault()?.Classes.FirstOrDefault());
            }
            else
            {
                PassTypes.Remove("Term Pass");
            }
            PassType = PassTypes.FirstOrDefault(type => type == defaultPassType, PassTypes.FirstOrDefault());
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
