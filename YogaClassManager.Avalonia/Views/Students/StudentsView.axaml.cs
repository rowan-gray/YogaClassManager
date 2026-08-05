using Avalonia.ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.Students;

namespace YogaClassManager.Avalonia.Views.Students;

public partial class StudentsView : ReactiveUserControl<StudentsViewModel>
{
    public StudentsView()
    {
        InitializeComponent();
    }
}
