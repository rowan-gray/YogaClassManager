using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Passes;

public partial class AddPassPage : ContentPage
{
    public AddPassPage(AddPassPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}