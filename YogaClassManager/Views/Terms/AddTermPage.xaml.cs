using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Terms;

public partial class AddTermPage : ContentPage
{
    public AddTermPage(AddTermPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}