using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.EmergencyContacts;

public partial class AddEmergencyContactPage : ContentPage
{
    public AddEmergencyContactPage(AddEmergencyContactPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}