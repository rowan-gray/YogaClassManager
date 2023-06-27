using YogaClassManager.Models.People.EventArguments;
using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.People;

public partial class PeoplePage : ContentPage
{
    public PeoplePage(PeoplePageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
        pageModel.ScrollToIndex += new ScrollToIndexEventHandler(ScrollToIndex);
    }
    private void ScrollToIndex(object source, ScrollToIndexEventArgs e)
    {
        PeopleList.ScrollTo(e.GetIndex(), animate: e.ShouldAnimate());
    }
    private void PeopleList_Scrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        if (e.LastVisibleItemIndex > ((PeoplePageModel)BindingContext).DisplayedCollection.Count - 6)
        {
            ((PeoplePageModel)BindingContext).EndOfListCommand.Execute(null);
        }
    }
}