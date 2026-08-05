using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     Lets the instructor pick which of a student's several eligible passes to charge for a roll
///     entry, when there's more than one - see MarkRollViewModel.AddSelectedStudentAsync, which builds
///     the already-ranked candidate list (Term before Dated before Casual, matching passes before
///     recently-expired ones) this just displays. Shaped like IdentityPickerViewModel but simpler - no
///     search, since the candidate set is already the full, small, pre-filtered list.
/// </summary>
public class PassPickerViewModel : DialogViewModelBase<Pass>
{
    private PassPickerRow? selectedRow;

    public PassPickerViewModel(IReadOnlyList<Pass> rankedCandidates, ClassSchedule schedule, DateOnly date,
        string studentName)
    {
        StudentName = studentName;
        Results = rankedCandidates
            .Select(pass => new PassPickerRow(pass, MarkRollViewModel.IsRecentlyExpired(pass, schedule, date)))
            .ToList();
        selectedRow = Results.FirstOrDefault();

        var canSelect = this.WhenAnyValue(x => x.SelectedRow).Select(r => r is not null);
        SelectCommand = ReactiveCommand.Create(() => Close(SelectedRow?.Pass), canSelect);
        DefaultCommand = SelectCommand;
    }

    public string StudentName { get; }
    public IReadOnlyList<PassPickerRow> Results { get; }

    /// <summary>Fixed for the life of the dialog - the candidate list is supplied whole at
    /// construction. In practice MarkRollViewModel only opens this with two or more candidates, so this
    /// is a guard against a future caller, not the normal path.</summary>
    public bool HasNoResults => Results.Count == 0;

    public PassPickerRow? SelectedRow
    {
        get => selectedRow;
        set => this.RaiseAndSetIfChanged(ref selectedRow, value);
    }

    public ReactiveCommand<Unit, Unit> SelectCommand { get; }
}

public record PassPickerRow(Pass Pass, bool IsExpired);
