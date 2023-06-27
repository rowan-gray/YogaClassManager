using YogaClassManager.Models.People.EventArguments;
using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Terms;

public partial class TermsPage : ContentPage
{
    public TermsPage(TermsPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
        pageModel.ScrollToIndex += new ScrollToIndexEventHandler(ScrollToIndex);
    }
    private void ScrollToIndex(object source, ScrollToIndexEventArgs e)
    {
        TermsList.ScrollTo(e.GetIndex(), animate: e.ShouldAnimate());
    }

    private void MainCollection_Scrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        if (e.LastVisibleItemIndex >= ((StudentsPageModel)BindingContext).DisplayedCollection.Count - 6)
        {
            ((StudentsPageModel)BindingContext).EndOfListCommand.Execute(null);
        }
    }
}