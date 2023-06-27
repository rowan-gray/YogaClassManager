using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Classes;

public partial class SelectClassPage : ContentPage
{
    public SelectClassPage(SelectClassPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}