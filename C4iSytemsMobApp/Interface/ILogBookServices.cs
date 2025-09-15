
using C4iSytemsMobApp.Models;

namespace C4iSytemsMobApp.Interface
{
    public interface ILogBookServices
    {        
        Task<HttpResponseMessage?> WriteEntryToLogBook(string _apiurl);
        Task<(bool isSuccess, string errorMessage)> LogActivityTask(string activityDescription, int scanningType = 0);
    }
}
