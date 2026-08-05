using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

public record ClassLinkSelection(ClassSchedule Schedule, int ClassCount);

/// <summary>Picks a ClassSchedule and a class count to link to a Term.</summary>
public class ClassLinkViewModel : DialogViewModelBase<ClassLinkSelection>
{
    private ClassSchedule? selectedSchedule;
    private int classCount = 10;
    private string? validationError;

    public ClassLinkViewModel(IClassScheduleRepository classScheduleRepository, string termName,
        IReadOnlyCollection<int> excludeScheduleIds)
    {
        TermName = termName;

        var canSave = this.WhenAnyValue(x => x.SelectedSchedule).Select(s => s is not null);
        SaveCommand = ReactiveCommand.Create(
            () => Close(SelectedSchedule is null ? null : new ClassLinkSelection(SelectedSchedule, ClassCount)),
            canSave);
        DefaultCommand = SaveCommand;

        LoadOnCreate(() => LoadSchedulesAsync(classScheduleRepository, excludeScheduleIds),
            exception => ValidationError = $"Couldn't load the list of classes: {exception.Message}");
    }

    public string TermName { get; }

    /// <summary>Surfaces a failed class-list load right beside the (then empty) dropdown, instead of
    /// leaving the user to guess why there's nothing to pick.</summary>
    public string? ValidationError
    {
        get => validationError;
        private set => this.RaiseAndSetIfChanged(ref validationError, value);
    }
    public ObservableCollection<ClassSchedule> AvailableSchedules { get; } = new();

    public ClassSchedule? SelectedSchedule
    {
        get => selectedSchedule;
        set => this.RaiseAndSetIfChanged(ref selectedSchedule, value);
    }

    public int ClassCount
    {
        get => classCount;
        set => this.RaiseAndSetIfChanged(ref classCount, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    private async Task LoadSchedulesAsync(IClassScheduleRepository classScheduleRepository,
        IReadOnlyCollection<int> excludeScheduleIds)
    {
        var schedules = await classScheduleRepository.Query(new ClassScheduleFilter()).LoadMultiple();

        AvailableSchedules.Clear();
        foreach (var schedule in schedules.Where(s => !excludeScheduleIds.Contains(s.Id)))
            AvailableSchedules.Add(schedule);
    }
}
