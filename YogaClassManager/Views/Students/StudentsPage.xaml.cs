using YogaClassManager.Models.People.EventArguments;
using YogaClassManager.ViewModels;

namespace YogaClassManager.Views;

public partial class StudentsPage : ContentPage
{
    public StudentsPage(StudentsPageModel pageModel)
    {
        InitializeComponent();
        this.BindingContext = pageModel;
        pageModel.ScrollToIndex += new ScrollToIndexEventHandler(ScrollToStudent);
    }

    private void ScrollToStudent(object source, ScrollToIndexEventArgs e)
    {
        StudentsList.ScrollTo(e.GetIndex(), animate: e.ShouldAnimate());
    }

    private void StudentsList_Scrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        if (e.LastVisibleItemIndex >= ((StudentsPageModel)BindingContext).DisplayedCollection.Count - 6)
        {
            ((StudentsPageModel)BindingContext).EndOfListCommand.Execute(null);
        }
    }
}