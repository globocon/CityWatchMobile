using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Models;


namespace C4iSytemsMobApp.Interface
{
    public interface ICustomLogEntryServices
    {
        Task ProcessCustomFieldLogsOnlineDataToCache(List<Dictionary<string, string?>> rows);
        Task<List<Dictionary<string, string?>>> GetCustomFieldLogsFromCache();
        Task<bool> SaveCustomFieldLogRequestHeadDataToCache(Dictionary<string,string?> toupdate);
    }
}
