using YogaClassManager.Models.People.EventArguments;
using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.People;

public partial class PeoplePage : ContentPage
{
    public PeoplePage(PeoplePageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
        //pageModel.ScrollToIndex += ScrollToIndex;
    }

    private void ScrollToIndex(object source, ScrollToIndexEventArgs e)
    {
        // TODO
        //PeopleList.ScrollTo(e.GetIndex(), animate: e.ShouldAnimate());
    }
}