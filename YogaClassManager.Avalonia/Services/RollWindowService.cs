using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Avalonia.Views.Shared;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.Services;

public class RollWindowService : IRollWindowService
{
    private readonly IClassRollRepository classRollRepository;
    private readonly IStudentRepository studentRepository;
    private readonly IPassRepository passRepository;
    private readonly ITermRepository termRepository;
    private readonly IToastService toastService;
    private readonly Dictionary<string, Window> openWindows = new();

    public RollWindowService(IClassRollRepository classRollRepository, IStudentRepository studentRepository,
        IPassRepository passRepository, ITermRepository termRepository, IToastService toastService)
    {
        this.classRollRepository = classRollRepository;
        this.studentRepository = studentRepository;
        this.passRepository = passRepository;
        this.termRepository = termRepository;
        this.toastService = toastService;
    }

    public void OpenOrActivate(ClassRoll roll, Action onClosed)
    {
        // A roll that hasn't been saved yet has Id 0 (MarkRollViewModel INSERTs on save), so keying
        // purely on Id would make every unsaved roll collide - opening a new roll for one class would
        // just re-activate an unsaved one belonging to a different class. Fall back to the thing that
        // does identify an unsaved roll: which class occurrence it is. The key is captured once and
        // reused by the Closed handler, so it stays correct even after a save assigns a real id.
        var key = roll.Id != 0
            ? $"roll:{roll.Id}"
            : $"unsaved:{roll.ClassSchedule.Id}:{roll.Date:yyyy-MM-dd}";

        if (openWindows.TryGetValue(key, out var existing))
        {
            existing.Activate();
            return;
        }

        var viewModel = new MarkRollViewModel(roll, classRollRepository, studentRepository, passRepository,
            termRepository, toastService);
        var window = new MarkRollWindow { DataContext = viewModel };

        openWindows[key] = window;
        window.Closed += (_, _) =>
        {
            openWindows.Remove(key);
            onClosed();
        };

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null)
            window.Show(owner);
        else
            window.Show();
    }
}
