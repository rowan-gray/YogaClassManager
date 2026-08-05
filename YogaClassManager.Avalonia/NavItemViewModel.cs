using System.Reactive;
using Material.Icons;
using ReactiveUI;

namespace YogaClassManager.Avalonia;

/// <summary>
///     A single entry in MainWindow's collapsible nav rail: an icon + label bound to a navigation
///     command, with IsSelected tracking whether it's the currently routed page.
/// </summary>
public class NavItemViewModel : ReactiveObject
{
    private bool isSelected;

    public NavItemViewModel(string label, MaterialIconKind icon,
        ReactiveCommand<Unit, IRoutableViewModel> command, string urlPathSegment)
    {
        Label = label;
        Icon = icon;
        Command = command;
        UrlPathSegment = urlPathSegment;
    }

    public string Label { get; }
    public MaterialIconKind Icon { get; }
    public ReactiveCommand<Unit, IRoutableViewModel> Command { get; }

    /// <summary>Matched against the router's current IRoutableViewModel.UrlPathSegment to drive IsSelected.</summary>
    public string UrlPathSegment { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => this.RaiseAndSetIfChanged(ref isSelected, value);
    }
}
