using Avalonia.ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.Dashboard;

namespace YogaClassManager.Avalonia.Views.Dashboard;

public partial class DashboardView : ReactiveUserControl<DashboardViewModel>
{
    public DashboardView()
    {
        InitializeComponent();
    }
}
