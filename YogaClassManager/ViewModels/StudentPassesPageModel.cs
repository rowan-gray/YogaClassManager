using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Database;
using YogaClassManager.Models;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.People;
using YogaClassManager.Services;
using YogaClassManager.ViewModels.Base;

namespace YogaClassManager.ViewModels
{
    public partial class StudentPassesPageModel : SearchableCollectionPageModel<Pass>, IQueryAttributable
    {
        bool queryAttributesApplied = false;
        [ObservableProperty]
        private Student student;
        PassesService passesService => databaseManager.PassesService;
        StudentsService studentService => databaseManager.StudentsService;
        public StudentPassesPageModel(DatabaseManager databaseManager, PopupService popupService) : base(databaseManager, popupService, 20, false)
        {
        }

        protected override Task<List<int>> GetDeletedIds()
        {
            return new(() => new()); 
        }

        protected override Task<List<Pass>> GetSearchedUpdatedItems(string query)
        {
            return new(() => new());
        }

        protected override Task<List<Pass>> GetUnsearchedUpdatedItems()
        {
            return new(() => new());
        }

        protected override Task<List<Pass>> RetrieveSearchedCollection(string query)
        {
            return new(() => new());
        }

        protected override Task<List<Pass>> RetrieveUnsearchedCollection()
        {
            return studentService.GetStudentsPasses(cancellationToken.Token, Student.Id, true, true);
        }

        protected override List<Pass> SortCollection(List<Pass> collection)
        {
            return collection;
        }

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
                    throw new ArgumentException("Student must be provided");

                queryAttributesApplied = true;
            }
        }
    }
}
