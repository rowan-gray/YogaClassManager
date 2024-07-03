using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Models.Passes;

public partial class Pass : ObservableObject, IUpdateable<Pass>, IIdentifiable
{
    [ObservableProperty] private ObservableCollection<PassAlteration> alterations;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ClassesRemaining))]
    public int classesUsed;

    public Pass(int id, int studentId, int classesUsed, ObservableCollection<PassAlteration> alterations)
    {
        Id = id;
        StudentId = studentId;
        ClassesUsed = classesUsed;
        Alterations = alterations;
        Alterations.CollectionChanged += AlterationsChanged;
    }

    public int StudentId { get; set; }
    public virtual int NumberOfClasses { get; set; }
    public virtual bool IsExpired => false;

    public int ClassesRemaining => NumberOfClasses - ClassesUsed;
    public virtual string PassName { get; }

    public int Id { get; set; }

    public void Update(Pass updatedData)
    {
        throw new NotImplementedException();
    }

    private void AlterationsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ClassesRemaining));
    }

    public static Pass Copy(Pass pass)
    {
        ObservableCollection<PassAlteration> alterationsCopy = new();

        foreach (var alteration in pass.Alterations) alterationsCopy.Add(PassAlteration.Copy(alteration));

        return new Pass(pass.Id, pass.StudentId, pass.ClassesUsed, alterationsCopy);
    }

    public virtual bool IsValid()
    {
        return true;
    }

    public virtual int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
    {
        return null;
    }
}