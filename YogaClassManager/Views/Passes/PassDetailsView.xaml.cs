using System.Windows.Input;
using YogaClassManager.Models.Passes;

namespace YogaClassManager.Views.Passes;

public partial class PassDetailsView : ContentView
{
    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(PassDetailsView));

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(PassDetailsView.EditCommandProperty);
        set => SetValue(PassDetailsView.EditCommandProperty, value);
    }

    public static readonly BindableProperty PassProperty = BindableProperty.Create(nameof(Pass), typeof(Pass), typeof(PassDetailsView));

    public Pass Pass
    {
        get => (Pass)GetValue(PassDetailsView.PassProperty);
        set => SetValue(PassDetailsView.PassProperty, value);
    }

    public PassDetailsView()
    {
        InitializeComponent();
    }
}