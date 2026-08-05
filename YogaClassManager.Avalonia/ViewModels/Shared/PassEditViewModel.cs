using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     Add/Edit form for a pass - one ViewModel drives all three pass types via a Kind switch (see
///     PassEditView's OptionSwitchPanel), mirroring the MAUI app's AddPassPageModel but without the
///     3x-duplicated type-dependent XAML that pattern led to there.
/// </summary>
public class PassEditViewModel : DialogViewModelBase<Pass>
{
    private readonly IDialogService dialogService;
    private readonly ITermRepository termRepository;
    private readonly int studentId;
    private readonly int passId;

    private PassKind kind;
    private int classCount = 1;
    private int classesUsed;
    private DateOnly startDate;
    private DateOnly endDate;
    private Term? selectedTerm;
    private TermClassSchedule? selectedTermClass;
    private string? validationError;

    public PassEditViewModel(int studentId, IDialogService dialogService, ITermRepository termRepository,
        Pass? editingPass = null)
    {
        this.studentId = studentId;
        this.dialogService = dialogService;
        this.termRepository = termRepository;
        IsNew = editingPass is null;
        passId = editingPass?.Id ?? 0;

        var today = DateOnly.FromDateTime(DateTime.Now);
        startDate = today;
        endDate = today.AddMonths(1);

        if (editingPass is not null)
        {
            classesUsed = editingPass.ClassesUsed;
            foreach (var alteration in editingPass.Alterations)
                Alterations.Add(alteration);

            switch (editingPass)
            {
                case CasualPass casual:
                    kind = PassKind.Casual;
                    classCount = casual.ClassCount;
                    break;
                case DatedPass dated:
                    kind = PassKind.Dated;
                    classCount = dated.ClassCount;
                    startDate = dated.StartDate;
                    endDate = dated.EndDate;
                    break;
                case TermPass termPass:
                    kind = PassKind.Term;
                    selectedTerm = termPass.Term;
                    selectedTermClass = termPass.TermClassSchedule;
                    break;
            }
        }
        else
        {
            kind = PassKind.Casual;
        }

        SaveCommand = ReactiveCommand.Create(Save);
        DefaultCommand = SaveCommand;
        AddAlterationCommand = ReactiveCommand.CreateFromTask(AddAlterationAsync);
        RemoveAlterationCommand = ReactiveCommand.Create<PassAlteration>(RemoveAlteration);

        Alterations.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(ClassesRemainingPreview));
        this.WhenAnyValue(x => x.Kind, x => x.ClassCount, x => x.ClassesUsed, x => x.SelectedTermClass)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ClassesRemainingPreview)));

        this.WhenAnyValue(x => x.SelectedTerm).Subscribe(term =>
        {
            AvailableTermClasses.Clear();
            if (term is not null)
                foreach (var termClass in term.Classes)
                    AvailableTermClasses.Add(termClass);

            if (SelectedTermClass is null || term is null || !term.Classes.Contains(SelectedTermClass))
                SelectedTermClass = AvailableTermClasses.FirstOrDefault();
        });

        LoadOnCreate(LoadTermsAsync,
            exception => ValidationError = $"Couldn't load the list of terms: {exception.Message}");
    }

    public bool IsNew { get; }
    public string Title => IsNew ? "Add pass" : "Edit pass";
    public IReadOnlyList<PassKind> KindOptions { get; } = [PassKind.Casual, PassKind.Dated, PassKind.Term];

    public PassKind Kind
    {
        get => kind;
        set => this.RaiseAndSetIfChanged(ref kind, value);
    }

    /// <summary>Doubles as the "single-use pass" case when set to 1 for a Casual pass - no separate type exists.</summary>
    public int ClassCount
    {
        get => classCount;
        set => this.RaiseAndSetIfChanged(ref classCount, value);
    }

    public int ClassesUsed
    {
        get => classesUsed;
        set => this.RaiseAndSetIfChanged(ref classesUsed, value);
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

    public ObservableCollection<Term> Terms { get; } = new();
    public ObservableCollection<TermClassSchedule> AvailableTermClasses { get; } = new();

    public Term? SelectedTerm
    {
        get => selectedTerm;
        set => this.RaiseAndSetIfChanged(ref selectedTerm, value);
    }

    public TermClassSchedule? SelectedTermClass
    {
        get => selectedTermClass;
        set => this.RaiseAndSetIfChanged(ref selectedTermClass, value);
    }

    public ObservableCollection<PassAlteration> Alterations { get; } = new();

    public int ClassesRemainingPreview
    {
        get
        {
            var total = Kind switch
            {
                PassKind.Casual => ClassCount,
                PassKind.Dated => ClassCount,
                PassKind.Term => SelectedTermClass?.ClassCount ?? 0,
                _ => 0
            };

            total += Alterations.Sum(a => a.Amount);
            return total - ClassesUsed;
        }
    }

    public string? ValidationError
    {
        get => validationError;
        private set => this.RaiseAndSetIfChanged(ref validationError, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> AddAlterationCommand { get; }
    public ReactiveCommand<PassAlteration, Unit> RemoveAlterationCommand { get; }

    private async Task LoadTermsAsync()
    {
        var terms = await termRepository.Query(new TermFilter { IncludeCompleted = true }).LoadMultiple();

        Terms.Clear();
        foreach (var term in terms)
            Terms.Add(term);

        if (SelectedTerm is null)
            SelectedTerm = Terms.FirstOrDefault();
    }

    private async Task AddAlterationAsync()
    {
        var alterationViewModel = new AlterationEditViewModel(passId);
        var alteration = await dialogService.ShowDialogAsync(alterationViewModel);

        if (alteration is not null)
            Alterations.Add(alteration);
    }

    private void RemoveAlteration(PassAlteration alteration)
    {
        Alterations.Remove(alteration);
    }

    private void Save()
    {
        var alterations = new ObservableCollection<PassAlteration>(Alterations);

        Pass pass;
        switch (Kind)
        {
            case PassKind.Casual:
                pass = new CasualPass(passId, studentId, ClassCount, alterations, ClassesUsed);
                break;
            case PassKind.Dated:
                pass = new DatedPass(passId, studentId, ClassCount, alterations, ClassesUsed, StartDate, EndDate);
                break;
            case PassKind.Term:
                if (SelectedTerm is null || SelectedTermClass is null)
                {
                    ValidationError = "Select a term and a class.";
                    return;
                }

                pass = new TermPass(passId, studentId, ClassesUsed, alterations, SelectedTerm, SelectedTermClass);
                break;
            default:
                ValidationError = "Select a pass type.";
                return;
        }

        if (!pass.IsValid())
        {
            ValidationError = "Check the pass details - the class count, dates, and remaining classes must be valid.";
            return;
        }

        Close(pass);
    }
}
