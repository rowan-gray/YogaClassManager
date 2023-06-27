using YogaClassManager.Models.People.EventArguments;
using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Passes;

public partial class StudentPassesPage : ContentPage
{
	public StudentPassesPage(StudentPassesPageModel pageModel)
	{
		InitializeComponent();
		BindingContext = pageModel;
        pageModel.ScrollToIndex += new ScrollToIndexEventHandler(ScrollToIndex);
    }

    private void ScrollToIndex(object source, ScrollToIndexEventArgs e)
    {
        MainCollection.ScrollTo(e.GetIndex(), animate: e.ShouldAnimate());
    }

    private void MainCollectionScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        if (e.LastVisibleItemIndex >= ((StudentPassesPageModel)BindingContext).DisplayedCollection.Count - 6)
        {
            ((StudentPassesPageModel)BindingContext).EndOfListCommand.Execute(null);
        }
    }
}