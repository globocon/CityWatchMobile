
using C4iSytemsMobApp.Models;

namespace C4iSytemsMobApp.Interface
{
    public interface ILogBookServices
    {        
        Task<HttpResponseMessage?> WriteEntryToLogBook(string _apiurl);
        Task<(bool isSuccess, string errorMessage)> LogActivityTask(string activityDescription, int scanningType = 0, string tagUID = "NA", bool IsSystemEntry = false, int NFCScannedFromSiteId = -1);
        Task<Dictionary<string, string>> GetCustomFieldConfigAsync();
        Task<List<Dictionary<string, string?>>> GetCustomFieldLogsAsync();
        Task<(bool isSuccess, string errorMessage)> SaveCustomFieldLogAsync(Dictionary<string, string> record);
        Task<List<PatrolCarLog>> GetPatrolCarLogsAsync();
        Task<(bool isSuccess, string errorMessage)> SavePatrolCarLogAsync(PatrolCarLog record);
    }
}
