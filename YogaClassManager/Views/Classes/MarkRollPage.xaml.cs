using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Classes;

public partial class MarkRollPage : ContentPage
{
    public MarkRollPage(MarkRollPageModel markRollPageModel)
    {
        InitializeComponent();
        BindingContext = markRollPageModel;
        markRollPageModel.RequestSearchBarFocus += SearchBarFocusRequested;
    }

    private void SearchBarFocusRequested(object source, EventArgs e)
    {
        studentSearch.Focus();
    }
}