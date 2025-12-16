
using C4iSytemsMobApp.Models;

namespace C4iSytemsMobApp.Interface
{
    public interface IGuardApiServices
    {        
        //Task<HttpResponseMessage?> WriteEntryToLogBook(string _apiurl);
        //Task<(bool isSuccess, string errorMessage)> LogActivityTask(string activityDescription, int scanningType = 0, string tagUID = "NA", bool IsSystemEntry = true);
        //Task<Dictionary<string, string>> GetCustomFieldConfigAsync();
        Task<List<SelectListItem>> GetStatesAsync();
        Task<(bool isSuccess, string errorMessage, NewGuard? _newGuard)> RegisterNewGuardAsync(NewGuard newGuard);
        //Task<(bool isSuccess, string errorMessage)> SaveCustomFieldLogAsync(Dictionary<string, string> record);
        //Task<List<PatrolCarLog>> GetPatrolCarLogsAsync();
        //Task<(bool isSuccess, string errorMessage)> SavePatrolCarLogAsync(PatrolCarLog record);
    }
}
