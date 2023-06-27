using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.People;

public partial class SearchPeoplePage : ContentPage
{
    public SearchPeoplePage(SearchPeoplePageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}