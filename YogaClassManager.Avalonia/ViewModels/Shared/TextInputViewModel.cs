using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>A generic single-line text prompt dialog - used for health concerns, alteration reasons, etc.</summary>
public class TextInputViewModel : DialogViewModelBase<string>
{
    private string value;

    public TextInputViewModel(string title, string label, string initialValue = "")
    {
        Title = title;
        Label = label;
        value = initialValue;

        var canSave = this.WhenAnyValue(x => x.Value).Select(v => !string.IsNullOrWhiteSpace(v));
        SaveCommand = ReactiveCommand.Create(() => Close(Value.Trim()), canSave);
        DefaultCommand = SaveCommand;
    }

    public string Title { get; }
    public string Label { get; }

    public string Value
    {
        get => value;
        set => this.RaiseAndSetIfChanged(ref this.value, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
}
