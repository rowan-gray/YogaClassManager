using YogaClassManager.ViewModels;

namespace YogaClassManager.Views;

public partial class EditDetailsPage : ContentPage
{
    public EditDetailsPage(EditDetailsPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}