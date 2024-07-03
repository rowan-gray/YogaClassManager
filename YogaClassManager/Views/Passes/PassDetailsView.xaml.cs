using System.Windows.Input;
using YogaClassManager.Models.Passes;

namespace YogaClassManager.Views.Passes;

public partial class PassDetailsView : ContentView
{
    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(PassDetailsView));

    public static readonly BindableProperty PassProperty =
        BindableProperty.Create(nameof(Pass), typeof(Pass), typeof(PassDetailsView));

    public PassDetailsView()
    {
        InitializeComponent();
    }

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public Pass Pass
    {
        get => (Pass)GetValue(PassProperty);
        set => SetValue(PassProperty, value);
    }
}