using Avalonia.ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.ClassSchedules;

namespace YogaClassManager.Avalonia.Views.ClassSchedules;

public partial class ClassSchedulesView : ReactiveUserControl<ClassSchedulesViewModel>
{
    public ClassSchedulesView()
    {
        InitializeComponent();
    }
}
