using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Students;

public partial class AddStudentPage : ContentPage
{
    public AddStudentPage(AddStudentPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}