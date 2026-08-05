using System.Reactive;
using ReactiveUI;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     Add/edit one ClassSchedule. Checks the repository's day/time uniqueness rule here rather than
///     letting AddAsync/UpdateAsync throw it: the dialog has already closed by then, so the user got an
///     error toast with all of their input discarded and nothing to correct.
/// </summary>
public class ClassScheduleEditViewModel : DialogViewModelBase<ClassSchedule>
{
    private readonly IClassScheduleRepository classScheduleRepository;
    private readonly int id;
    private DayOfWeek day;
    private TimeSpan time;
    private bool isArchived;
    private string? validationError;

    public ClassScheduleEditViewModel(IClassScheduleRepository classScheduleRepository,
        ClassSchedule? editingSchedule = null)
    {
        this.classScheduleRepository = classScheduleRepository;

        IsNew = editingSchedule is null;
        id = editingSchedule?.Id ?? 0;
        day = editingSchedule?.Day ?? DayOfWeek.Monday;
        time = editingSchedule?.Time.ToTimeSpan() ?? new TimeSpan(9, 0, 0);
        isArchived = editingSchedule?.IsArchived ?? false;

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        DefaultCommand = SaveCommand;
    }

    public string? ValidationError
    {
        get => validationError;
        private set => this.RaiseAndSetIfChanged(ref validationError, value);
    }

    public bool IsNew { get; }
    public string Title => IsNew ? "Add class" : "Edit class";
    public IReadOnlyList<DayOfWeek> DayOptions { get; } = Enum.GetValues<DayOfWeek>();

    public DayOfWeek Day
    {
        get => day;
        set => this.RaiseAndSetIfChanged(ref day, value);
    }

    public TimeSpan Time
    {
        get => time;
        set => this.RaiseAndSetIfChanged(ref time, value);
    }

    public bool IsArchived
    {
        get => isArchived;
        set => this.RaiseAndSetIfChanged(ref isArchived, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    private async Task SaveAsync()
    {
        ValidationError = null;

        var candidateTime = TimeOnly.FromTimeSpan(Time);

        // IncludeArchived: an archived class still occupies its slot as far as the repository's
        // uniqueness check is concerned, so ignoring archived rows here would let the dialog pass
        // something AddAsync then rejects - exactly the round trip this check exists to avoid.
        var sameDay = await classScheduleRepository
            .Query(new ClassScheduleFilter { Day = Day, IncludeArchived = true })
            .LoadMultiple();

        if (sameDay.Any(schedule => schedule.Id != id && schedule.Time == candidateTime))
        {
            ValidationError = $"There's already a class on {Day} at {candidateTime:HH:mm}.";
            return;
        }

        Close(new ClassSchedule(id, Day, candidateTime, IsArchived));
    }
}
