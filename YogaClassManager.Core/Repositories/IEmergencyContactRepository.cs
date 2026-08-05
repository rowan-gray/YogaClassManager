using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Repositories;

public interface IEmergencyContactRepository
{
    IDbModel<EmergencyContact, EmergencyContactFilter> Query(EmergencyContactFilter filter);
}
