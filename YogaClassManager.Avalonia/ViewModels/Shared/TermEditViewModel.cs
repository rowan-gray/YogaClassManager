using System.Reactive;
using ReactiveUI;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

public class TermEditViewModel : DialogViewModelBase<Term>
{
    private readonly int id;
    private readonly List<TermClassSchedule> classes;
    private string name;
    private DateOnly startDate;
    private DateOnly endDate;
    private bool hasCatchup;
    private DateOnly catchupStartDate;
    private DateOnly catchupEndDate;
    private string? validationError;

    public TermEditViewModel(Term? editingTerm = null)
    {
        IsNew = editingTerm is null;
        id = editingTerm?.Id ?? 0;
        classes = editingTerm?.Classes.ToList() ?? [];

        var today = DateOnly.FromDateTime(DateTime.Now);
        name = editingTerm?.Name ?? "";
        startDate = editingTerm?.StartDate ?? today;
        endDate = editingTerm?.EndDate ?? today.AddMonths(3);
        hasCatchup = editingTerm?.CatchupStartDate is not null;
        catchupStartDate = editingTerm?.CatchupStartDate ?? startDate.AddDays(-7);
        catchupEndDate = editingTerm?.CatchupEndDate ?? endDate.AddDays(7);

        SaveCommand = ReactiveCommand.Create(Save);
        DefaultCommand = SaveCommand;
    }

    public bool IsNew { get; }
    public string Title => IsNew ? "Add term" : "Edit term";

    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }

    public DateOnly StartDate
    {
        get => startDate;
        set => this.RaiseAndSetIfChanged(ref startDate, value);
    }

    public DateOnly EndDate
    {
        get => endDate;
        set => this.RaiseAndSetIfChanged(ref endDate, value);
    }

    public bool HasCatchup
    {
        get => hasCatchup;
        set => this.RaiseAndSetIfChanged(ref hasCatchup, value);
    }

    public DateOnly CatchupStartDate
    {
        get => catchupStartDate;
        set => this.RaiseAndSetIfChanged(ref catchupStartDate, value);
    }

    public DateOnly CatchupEndDate
    {
        get => catchupEndDate;
        set => this.RaiseAndSetIfChanged(ref catchupEndDate, value);
    }

    public string? ValidationError
    {
        get => validationError;
        private set => this.RaiseAndSetIfChanged(ref validationError, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    private void Save()
    {
        var term = new Term(id, Name, StartDate, EndDate,
            HasCatchup ? CatchupStartDate : null,
            HasCatchup ? CatchupEndDate : null,
            classes);

        if (!term.IsValid())
        {
            ValidationError = "Check the name and dates - the start date must be before the end date, and " +
                               "catchup dates (if set) must bracket the term.";
            return;
        }

        Close(term);
    }
}
