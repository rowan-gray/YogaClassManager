using YogaClassManager.ViewModels;

namespace YogaClassManager.Views.Terms;

public partial class EditTermPage : ContentPage
{
	public EditTermPage(EditTermPageModel pageModel)
	{
		InitializeComponent();
		BindingContext = pageModel;
	}
}