using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.People;
using YogaClassManager.Services;
using YogaClassManager.ViewModels.Base;

namespace YogaClassManager.ViewModels;

public partial class StudentPassesPageModel : SearchableCollectionPageModel<Pass>, IQueryAttributable
{
    private bool queryAttributesApplied;

    [ObservableProperty] private Student student;

    public StudentPassesPageModel(DatabaseManager databaseManager, PopupService popupService) : base(databaseManager,
        popupService, 20, false)
    {
        BackCommand = new Command(async () => { await NavigationService.GoBackAsync(); });
    }

    private PassesService passesService => databaseManager.PassesService;
    private StudentsService studentService => databaseManager.StudentsService;
    public Command BackCommand { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!queryAttributesApplied)
        {
            if (query.ContainsKey("student"))
            {
                Student = (Student)((Message)query["student"]).Parameter;

                RetrieveCollection();
            }
            else
            {
                throw new ArgumentException("Student must be provided");
            }

            queryAttributesApplied = true;
        }
    }

    protected override Task<List<int>> GetDeletedIds()
    {
        return new Task<List<int>>(() => new List<int>());
    }

    protected override Task<List<Pass>> GetSearchedUpdatedItems(string query)
    {
        return new Task<List<Pass>>(() => new List<Pass>());
    }

    protected override Task<List<Pass>> GetUnsearchedUpdatedItems()
    {
        return new Task<List<Pass>>(() => new List<Pass>());
    }

    protected override Task<List<Pass>> RetrieveSearchedCollection(string query)
    {
        return new Task<List<Pass>>(() => new List<Pass>());
    }

    protected override Task<List<Pass>> RetrieveUnsearchedCollection()
    {
        return studentService.GetStudentsPasses(cancellationToken.Token, Student.Id, true, true);
    }

    protected override List<Pass> SortCollection(List<Pass> collection)
    {
        return collection;
    }
}