using YogaClassManager.ViewModels;

namespace YogaClassManager;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageModel mainPageModel)
    {
        InitializeComponent();
        BindingContext = mainPageModel;
    }
}