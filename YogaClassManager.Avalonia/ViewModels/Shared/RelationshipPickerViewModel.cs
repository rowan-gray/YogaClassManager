using System.Reactive;
using ReactiveUI;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>Picks a Relationship for a newly-linked emergency contact.</summary>
public class RelationshipPickerViewModel : DialogViewModelBase<Relationship?>
{
    private Relationship selected = Relationship.Parent;

    public RelationshipPickerViewModel(string identityName)
    {
        IdentityName = identityName;
        SaveCommand = ReactiveCommand.Create(() => Close(Selected));
        DefaultCommand = SaveCommand;
    }

    public string IdentityName { get; }
    public IReadOnlyList<Relationship> Options { get; } = Enum.GetValues<Relationship>();

    public Relationship Selected
    {
        get => selected;
        set => this.RaiseAndSetIfChanged(ref selected, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
}
