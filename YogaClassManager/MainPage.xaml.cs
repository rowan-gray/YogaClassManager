namespace YogaClassManager;

public partial class MainPage : ContentPage
{
    public MainPage(ViewModels.MainPageModel mainPageModel)
    {
        InitializeComponent();
        BindingContext = mainPageModel;
    }
}


