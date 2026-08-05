using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Models.Passes;

public partial class Pass : ObservableObject, IIdentifiable
{
    [ObservableProperty] private int id;

    [ObservableProperty] private int studentId;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ClassesRemaining)), NotifyPropertyChangedFor(nameof(IsDepleted))]
    private int classesUsed;

    [ObservableProperty] private ObservableCollection<PassAlteration> alterations;

    public Pass(int id, int studentId, int classesUsed, ObservableCollection<PassAlteration> alterations)
    {
        Id = id;
        StudentId = studentId;
        ClassesUsed = classesUsed;
        this.alterations = alterations;
        alterations.CollectionChanged += AlterationsChanged;
    }

    public virtual int NumberOfClasses { get; }
    public virtual bool IsExpired => false;
    public virtual string PassName => "Pass";

    /// <summary>Formatted effective-to-expiry range for passes that have one (e.g. DatedPass); null
    /// otherwise. Exists so UI can bind to it directly without a type cast - Avalonia's classic
    /// (non-compiled) bindings, used throughout this app, can't resolve XAML type-cast binding paths.</summary>
    public virtual string? DateRangeDisplay => null;

    /// <summary>The date this pass stops being usable, for passes that have one; null otherwise (e.g.
    /// CasualPass, which only depletes by class count). Mirrors the date each subtype's own IsExpired
    /// getter compares against, so the two stay consistent by construction.</summary>
    public virtual DateOnly? ExpiryDate => null;

    public int ClassesRemaining => NumberOfClasses - ClassesUsed;
    public bool IsDepleted => ClassesRemaining <= 0;

    private void AlterationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ClassesRemaining));
        OnPropertyChanged(nameof(IsDepleted));
    }

    public static Pass Copy(Pass pass)
    {
        var alterationsCopy = new ObservableCollection<PassAlteration>(pass.Alterations.Select(PassAlteration.Copy));
        return new Pass(pass.Id, pass.StudentId, pass.ClassesUsed, alterationsCopy);
    }

    public virtual bool IsValid()
    {
        return true;
    }

    /// <summary>
    ///     Ranks how strongly this pass should be auto-selected for the given class/date during roll marking.
    ///     Lower is higher priority; null means this pass cannot be used for that class/date.
    /// </summary>
    public virtual int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
    {
        return null;
    }

    public override string ToString()
    {
        return PassName;
    }
}
