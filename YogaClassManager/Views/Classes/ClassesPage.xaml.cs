using YogaClassManager.Models.People.EventArguments;
using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Classes;

public partial class ClassesPage : ContentPage
{
    public ClassesPage(ClassesPageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();

        pageModel.ScrollToIndex += new ScrollToIndexEventHandler(ScrollToIndex);
    }
    private void ScrollToIndex(object source, ScrollToIndexEventArgs e)
    {
        //StudentsList.ScrollTo(e.GetIndex(), animate: e.ShouldAnimate());
    }

    private void MainCollection_Scrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        if (e.LastVisibleItemIndex >= ((StudentsPageModel)BindingContext).DisplayedCollection.Count - 6)
        {
            ((StudentsPageModel)BindingContext).EndOfListCommand.Execute(null);
        }
    }
}