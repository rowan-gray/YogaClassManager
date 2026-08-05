using System.Reactive;
using ReactiveUI;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     Shown when Add-Student's name-match check finds an existing, non-Student Identity that looks
///     like the same person - asks whether to link/convert that existing record instead of creating a
///     new one. Distinct from MergeConfirmViewModel (used by Identities' "merge duplicates" flow):
///     there, a second persisted row is deleted once its data is repointed; here there's no second
///     row at all yet (the form data hasn't been saved), so the wording and shape differ.
/// </summary>
public class PromoteConfirmViewModel : DialogViewModelBase<bool>
{
    public PromoteConfirmViewModel(Identity existingMatch, Student newFormData)
    {
        ExistingMatch = existingMatch;
        NewFormData = newFormData;

        ConfirmCommand = ReactiveCommand.Create(() => Close(true));
        DefaultCommand = ConfirmCommand;
    }

    public Identity ExistingMatch { get; }
    public Student NewFormData { get; }

    public string Summary =>
        $"{ExistingMatch.FullName} already exists as an identity. Use this existing record instead " +
        $"of creating a new one?\n\nTheir contact details will be updated to what you just entered.";

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
}
