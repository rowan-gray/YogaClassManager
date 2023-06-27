using YogaClassManager.ViewModels;

namespace YogaClassManager.Views;

public partial class SettingsPage : ContentPage
{
	public SettingsPage(SettingsPageModel pageModel)
	{
		InitializeComponent();
		BindingContext = pageModel;
	}
}