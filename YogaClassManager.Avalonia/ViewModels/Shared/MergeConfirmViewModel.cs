using System.Reactive;
using ReactiveUI;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>Shows what merging two identities will do before it happens - the "preview" step of the merge flow.</summary>
public class MergeConfirmViewModel : DialogViewModelBase<bool>
{
    public MergeConfirmViewModel(Identity survivor, Identity duplicate, IdentityLinkageSummary duplicateLinkage)
    {
        Survivor = survivor;
        Duplicate = duplicate;
        DuplicateLinkage = duplicateLinkage;

        ConfirmCommand = ReactiveCommand.Create(() => Close(true));
        DefaultCommand = ConfirmCommand;
    }

    public Identity Survivor { get; }
    public Identity Duplicate { get; }
    public IdentityLinkageSummary DuplicateLinkage { get; }

    public string Summary =>
        $"{Duplicate.FullName} will be merged into {Survivor.FullName}.\n\n" +
        $"{Duplicate.FullName} is currently:\n" +
        $"  - a Student: {(DuplicateLinkage.IsStudent ? "yes" : "no")}\n" +
        $"  - linked as an emergency contact: {DuplicateLinkage.EmergencyContactLinkCount} time(s)\n" +
        $"  - holding passes: {DuplicateLinkage.PassCount}\n" +
        $"  - with attendance records: {DuplicateLinkage.AttendanceCount}\n\n" +
        $"All of the above will be repointed to {Survivor.FullName}, and the {Duplicate.FullName} record " +
        "will then be removed.";

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
}
