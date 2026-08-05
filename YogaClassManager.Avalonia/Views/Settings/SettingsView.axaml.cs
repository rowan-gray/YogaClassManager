using Avalonia.ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.Settings;

namespace YogaClassManager.Avalonia.Views.Settings;

public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
