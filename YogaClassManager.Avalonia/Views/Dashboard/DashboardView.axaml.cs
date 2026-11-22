using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.Dashboard;

namespace YogaClassManager.Avalonia.Views.Dashboard;

public partial class DashboardView : ReactiveUserControl<DashboardViewModel>
{
    public DashboardView()
    {
        InitializeComponent();

        // The banner is a plain Border, not a Button: button chrome (hover/pressed backgrounds) would
        // give the easter egg away and paint over the image.
        Banner.Tapped += (_, _) => ViewModel?.PlayNootCommand.Execute().Subscribe();
    }
}
