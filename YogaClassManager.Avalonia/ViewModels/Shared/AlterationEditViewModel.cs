using System.Reactive;
using ReactiveUI;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>Add/edit a single PassAlteration (a manual +/- credit/refund adjustment on a pass).</summary>
public class AlterationEditViewModel : DialogViewModelBase<PassAlteration>
{
    private int amount;
    private string reason;
    private string? validationError;

    public AlterationEditViewModel(int passId)
    {
        PassId = passId;
        amount = 1;
        reason = "";

        SaveCommand = ReactiveCommand.Create(Save);
        DefaultCommand = SaveCommand;
    }

    public int PassId { get; }

    public int Amount
    {
        get => amount;
        set => this.RaiseAndSetIfChanged(ref amount, value);
    }

    public string Reason
    {
        get => reason;
        set => this.RaiseAndSetIfChanged(ref reason, value);
    }

    public string? ValidationError
    {
        get => validationError;
        private set => this.RaiseAndSetIfChanged(ref validationError, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    private void Save()
    {
        var alteration = new PassAlteration(0, PassId, Amount, Reason);

        if (!alteration.IsValid())
        {
            ValidationError = "Amount must not be zero.";
            return;
        }

        Close(alteration);
    }
}
