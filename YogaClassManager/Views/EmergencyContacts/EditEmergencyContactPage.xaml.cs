using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.EmergencyContacts;

public partial class EditEmergencyContactPage : ContentPage
{
    public EditEmergencyContactPage(EditEmergencyContactPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}