using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.Models.Classes;

public partial class Term : ObservableObject, IUpdateable<Term>, IIdentifiable
{
    [ObservableProperty] private DateOnly? catchupEndDate;

    [ObservableProperty] private DateOnly? catchupStartDate;

    [ObservableProperty] private ObservableCollection<TermClassSchedule> classes;

    [ObservableProperty] private DateOnly endDate;

    [ObservableProperty] private int id;

    [ObservableProperty] private string name;

    [ObservableProperty] private DateOnly startDate;

    public Term(int id, string name, DateOnly startDate, DateOnly endDate, DateOnly? catchupStartDate,
        DateOnly? catchupEndDate, List<TermClassSchedule> classes)
    {
        Id = id;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        CatchupStartDate = catchupStartDate;
        CatchupEndDate = catchupEndDate;
        Classes = new ObservableCollection<TermClassSchedule>(classes);
    }

    public void Update(Term updatedData)
    {
        Id = updatedData.Id;
        Name = updatedData.Name;
        StartDate = updatedData.StartDate;
        EndDate = updatedData.EndDate;
        CatchupStartDate = updatedData.CatchupStartDate;
        CatchupEndDate = updatedData.CatchupEndDate;
        Classes.Clear();
        foreach (var classSchedule in updatedData.Classes) Classes.Add(classSchedule);
    }

    public static Term Copy(Term term)
    {
        return (Term)term.MemberwiseClone();
    }

    public override string ToString()
    {
        return $"{Name}: {StartDate}-{EndDate}";
    }

    public bool IsValid()
    {
        return StartDate < EndDate && (CatchupStartDate is null || CatchupStartDate < StartDate) &&
               (CatchupEndDate is null || CatchupEndDate > EndDate) && Name is not null && Name?.Trim().Length != 0;
    }
}