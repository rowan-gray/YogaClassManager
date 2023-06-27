using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Classes;

public partial class EditClassDetailsPage : ContentPage
{
    public EditClassDetailsPage(EditClassDetailsPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}