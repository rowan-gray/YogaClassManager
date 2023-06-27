using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Classes;

public partial class AddClassPage : ContentPage
{
    public AddClassPage(AddClassPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}