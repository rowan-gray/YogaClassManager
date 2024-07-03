using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.People;
using YogaClassManager.Services;

namespace YogaClassManager.ViewModels;

[QueryProperty(nameof(CallbackParameter), "parameter1")]
public partial class SearchPeoplePageModel : ObservableObject
{
    private readonly DatabaseManager databaseManager;
    private readonly PopupService popupService;

    [ObservableProperty] private bool returnHiddenPeople;

    [ObservableProperty] private List<Person> searchedPeople;

    [ObservableProperty] private string searchQuery = "";

    private Person selectedPerson;

    public SearchPeoplePageModel(DatabaseManager databaseManager, PopupService popupService)
    {
        SelectCommand = new Command(SelectCommandExecute, SelectCommandCanExecute);
        CancelCommand = new Command(CancelCommandExecute);
        SearchPeopleCommand = new Command(SearchPeopleCommandExecute);
        this.databaseManager = databaseManager;
        this.popupService = popupService;
    }

    public Message CallbackParameter
    {
        set => Callback = (Action<Person>)value.Parameter;
    }

    public Action<Person> Callback { get; set; }

    public Command SelectCommand { get; set; }

    public Command CancelCommand { get; set; }

    public Command SearchPeopleCommand { get; set; }

    public Person SelectedPerson
    {
        get => selectedPerson;
        set
        {
            selectedPerson = value;
            OnPropertyChanged();
            SelectCommand?.ChangeCanExecute();
        }
    }

    private async void SearchPeopleCommandExecute()
    {
        if (SearchQuery is null)
            return;

        try
        {
            SearchedPeople =
                await databaseManager.PeopleService.SearchPeopleAsync(CancellationToken.None, SearchQuery,
                    ReturnHiddenPeople);

            SelectedPerson = null;
        }
        catch (TaskCanceledException)
        {
            await popupService.DisplayAlert("Operation Cancelled", "The previous operation was cancelled!", "Ok");
        }
        catch (Exception e)
        {
            await popupService.DisplayAlert("Database error",
                $"There was an error while trying to access the database.\n{e.Message}", "Ok");
        }
    }

    private async void SelectCommandExecute()
    {
        if (SelectedPerson is not null)
        {
            await NavigationService.GoBackAsync();
            Callback?.Invoke(SelectedPerson);
        }
    }

    private bool SelectCommandCanExecute()
    {
        return SelectedPerson is not null;
    }

    private async void CancelCommandExecute()
    {
        await NavigationService.GoBackAsync();
    }
}