using YogaClassManager.ViewModels;

namespace YogaClassManager.Views;

public partial class EditPassPage : ContentPage
{
    public EditPassPage(EditPassPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}