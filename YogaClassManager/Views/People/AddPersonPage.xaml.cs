using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.People;

public partial class AddPersonPage : ContentPage
{
    public AddPersonPage(AddPersonPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}