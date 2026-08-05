using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.Core.Models.Classes;

public partial class Term : ObservableObject, IIdentifiable, IUpdateable<Term>
{
    [ObservableProperty] private int id;

    [ObservableProperty] private string name = "";

    [ObservableProperty] private DateOnly startDate;

    [ObservableProperty] private DateOnly endDate;

    [ObservableProperty] private DateOnly? catchupStartDate;

    [ObservableProperty] private DateOnly? catchupEndDate;

    [ObservableProperty] private ObservableCollection<TermClassSchedule> classes = new();

    public Term(int id, string name, DateOnly startDate, DateOnly endDate, DateOnly? catchupStartDate,
        DateOnly? catchupEndDate, IEnumerable<TermClassSchedule> classes)
    {
        Id = id;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        CatchupStartDate = catchupStartDate;
        CatchupEndDate = catchupEndDate;
        Classes = new ObservableCollection<TermClassSchedule>(classes);
    }

    public bool IsCompleted => DateOnly.FromDateTime(DateTime.Now) > (CatchupEndDate ?? EndDate);

    public void Update(Term updatedData)
    {
        Name = updatedData.Name;
        StartDate = updatedData.StartDate;
        EndDate = updatedData.EndDate;
        CatchupStartDate = updatedData.CatchupStartDate;
        CatchupEndDate = updatedData.CatchupEndDate;
        Classes.Clear();
        foreach (var termClassSchedule in updatedData.Classes) Classes.Add(termClassSchedule);
    }

    public bool IsValid()
    {
        return StartDate < EndDate
               && (CatchupStartDate is null || CatchupStartDate < StartDate)
               && (CatchupEndDate is null || CatchupEndDate > EndDate)
               && Name.Trim().Length != 0;
    }

    public override string ToString()
    {
        return $"{Name}: {StartDate}-{EndDate}";
    }
}
